using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;

using Rhino;
using Rhino.Geometry;

using Grasshopper.Kernel;

using SandWorm.Analytics;
using static SandWorm.Core;
using static SandWorm.Kinect2Helpers;
using static SandWorm.Structs;
using static SandWorm.SandWormComponentUI;
using Microsoft.Azure.Kinect.Sensor;

namespace SandWorm
{
    public class SandWormComponent : GH_ExtendableComponent
    {
        #region Class Variables

        // Units & dimensions
        private Vector2 depthPixelSize;
        private double unitsMultiplier = 1;

        private int activeHeight = 0;
        private int activeWidth = 0;
        private int trimmedHeight;
        private int trimmedWidth;

        // Data arrays
        private BGRA[] rgbArray;
        private Point3d[] allPoints;
        private Color[] _vertexColors;
        private Mesh _quadMesh;
        private PointCloud _cloud;
        private List<Color> colorPalettes;

        private readonly LinkedList<int[]> renderBuffer = new LinkedList<int[]>();
        private int[] runningSum;
        private double[] elevationArray; // Array of elevation values for every pixel scanned during the calibration process
        private Vector2[] trimmedXYLookupTable = null;
        private Vector3?[] trimmedBooleanMatrix;
        private BGRA[] trimmedRGBArray;

        // Outputs
        private List<Mesh> _outputMesh;
        private List<GeometryBase> _outputWaterSurface;
        private List<Line> _outputContours;

        // Debugging
        public static List<string> stats;
        protected Stopwatch timer;

        // Boolean controls
        private bool calibrate;
        public bool reset;

        #endregion

        public SandWormComponent()
          : base("Sandworm Mesh", "SW Mesh",
            "Visualise Kinect depth data as a mesh", "SandWorm", "Visualisation")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Calibrate", "calibrate", "", GH_ParamAccess.item, calibrate);
            pManager.AddBooleanParameter("Reset", "reset", "Hitting this button will reset everything to defaults.", GH_ParamAccess.item, reset);
            pManager.AddColourParameter("Color list", "color list", "Provide 5 custom colors to define a gradient for the elevation analysis.", GH_ParamAccess.list);
            //pManager.AddGenericParameter("Mesh", "mesh", "", GH_ParamAccess.item);

            pManager[2].Optional = true;
            //pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Terrain", "terrain", "", GH_ParamAccess.list);
            pManager.AddGeometryParameter("Water surface", "water surface", "", GH_ParamAccess.list);
            pManager.AddCurveParameter("Contours", "contours", "", GH_ParamAccess.list);
            pManager.AddGenericParameter("Stats", "stats", "", GH_ParamAccess.item);
        }

        protected override void Setup(GH_ExtendableComponentAttributes attr) // Initialize the UI
        {
            MainComponentUI(attr);
        }

        protected override void OnComponentLoaded()
        {
            base.OnComponentLoaded();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DA.GetData(1, ref reset);
            if (reset || _reset)
            {
                KinectAzureController.sensor.Dispose();
                KinectAzureController.sensor = null;
                _quadMesh = null;
                trimmedXYLookupTable = null; // Force trimming of lookup tables
                _reset = false;
                renderBuffer.Clear();
                runningSum = null;

                // Only calculate once
                unitsMultiplier = GeneralHelpers.ConvertDrawingUnits(RhinoDoc.ActiveDoc.ModelUnitSystem);
                depthPixelSize = Kinect2Helpers.GetDepthPixelSpacing(_sensorElevation.Value);
            }

            if (_resize)
            {
                trimmedXYLookupTable = null;
                _quadMesh = null;
                renderBuffer.Clear();
                runningSum = null;
                _resize = false;
            }


            GeneralHelpers.SetupLogging(ref timer, ref stats);

            // Trim 
            GetTrimmedDimensions((KinectTypes)_sensorType.Value, ref trimmedWidth, ref trimmedHeight, ref elevationArray, runningSum,
                                  _bottomRows.Value, _topRows.Value, _leftColumns.Value, _rightColumns.Value);


            // Initialize
            int[] depthFrameDataInt = new int[trimmedWidth * trimmedHeight];
            double[] averagedDepthFrameData = new double[trimmedWidth * trimmedHeight];
            allPoints = new Point3d[trimmedWidth * trimmedHeight];
            _outputMesh = new List<Mesh>();
            _outputWaterSurface = new List<GeometryBase>();
            _outputContours = new List<Line>();

            if (runningSum == null || runningSum.Length < elevationArray.Length)
                runningSum = Enumerable.Range(1, elevationArray.Length).Select(i => new int()).ToArray();

            SetupRenderBuffer(depthFrameDataInt, (KinectTypes)_sensorType.Value, ref rgbArray, _analysisType.Value,
                _leftColumns.Value, _rightColumns.Value, _bottomRows.Value, _topRows.Value, _sensorElevation.Value, ref activeHeight, ref activeWidth,
                _quadMesh, trimmedWidth, trimmedHeight, _averagedFrames.Value,
                runningSum, renderBuffer);


            if ((KinectTypes)_sensorType.Value != KinectTypes.KinectForWindows && trimmedXYLookupTable == null) // Only recalculate on reset
            {
                trimmedXYLookupTable = new Vector2[trimmedWidth * trimmedHeight];
                trimmedBooleanMatrix = new Vector3?[trimmedWidth * trimmedHeight];

                Core.TrimXYLookupTable(KinectAzureController.idealXYCoordinates, trimmedXYLookupTable, KinectAzureController.verticalTiltCorrectionMatrix,
                    KinectAzureController.undistortMatrix, trimmedBooleanMatrix,
                                    _leftColumns.Value, _rightColumns.Value, _bottomRows.Value, _topRows.Value,
                                    activeHeight, activeWidth, unitsMultiplier);
            }

            GeneralHelpers.LogTiming(ref stats, timer, "Initial setup"); // Debug Info

            AverageAndBlurPixels(depthFrameDataInt, ref averagedDepthFrameData, runningSum, renderBuffer,
                _sensorElevation.Value, elevationArray, _averagedFrames.Value, _blurRadius.Value, trimmedWidth, trimmedHeight);

            GeneratePointCloud(averagedDepthFrameData, trimmedXYLookupTable, KinectAzureController.verticalTiltCorrectionMatrix, allPoints,
                renderBuffer, trimmedWidth, trimmedHeight, _sensorElevation.Value, unitsMultiplier, _averagedFrames.Value);

            // Produce 1st type of analysis that acts on the pixel array and assigns vertex colors
            if ((AnalysisTypes)_analysisType.Value == AnalysisTypes.RGB)
            {
                trimmedRGBArray = new BGRA[trimmedHeight * trimmedWidth];
                TrimColorArray(rgbArray, trimmedRGBArray,
                    _leftColumns.Value, _rightColumns.Value, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth);
            }

            colorPalettes = new List<Color>();
            DA.GetDataList(2, colorPalettes);

            GenerateMeshColors(ref _vertexColors, _analysisType.Value, averagedDepthFrameData,
                depthPixelSize, trimmedXYLookupTable, trimmedRGBArray,
                _colorGradientRange.Value, (Structs.ColorPalettes)_colorPalette.Value, colorPalettes,
                _sensorElevation.Value, trimmedWidth, trimmedHeight);

            GeneralHelpers.LogTiming(ref stats, timer, "Point cloud analysis"); // Debug Info

            if ((OutputTypes)_outputType.Value == OutputTypes.Mesh)
            {
                _cloud = null;
                // Generate the mesh
                _quadMesh = CreateQuadMesh(_quadMesh, allPoints, _vertexColors, trimmedBooleanMatrix, trimmedWidth, trimmedHeight);
                _outputMesh.Add(_quadMesh);

                GeneralHelpers.LogTiming(ref stats, timer, "Meshing"); // Debug Info

                // Produce 2nd type of analysis that acts on the mesh and creates new geometry
                if (_contourIntervalRange.Value > 0)
                {
                    ContoursFromPoints.GetGeometryForAnalysis(ref _outputContours, allPoints, (int)_contourIntervalRange.Value, trimmedWidth, trimmedHeight, (int)_contourRoughness.Value);
                    if (Params.Output[2].Recipients.Count > 0)
                        DA.SetDataList(2, _outputContours);
                }

                GeneralHelpers.LogTiming(ref stats, timer, "Mesh analysis"); // Debug Info
                DA.SetDataList(0, _outputMesh);
            }
            else if ((OutputTypes)_outputType.Value == OutputTypes.PointCloud)
            {
                _cloud = new PointCloud();

                if (_vertexColors.Length > 0)
                    _cloud.AddRange(allPoints, _vertexColors);
                else
                    _cloud.AddRange(allPoints);

                GeneralHelpers.LogTiming(ref stats, timer, "Point cloud display"); // Debug Info
            }

            if (_waterLevel.Value > 0)
            {
                WaterLevel.GetGeometryForAnalysis(ref _outputWaterSurface, _waterLevel.Value, allPoints, trimmedWidth);
                DA.SetDataList(1, _outputWaterSurface);
                GeneralHelpers.HideParameterGeometry(Params.Output[1]);
            }

            DA.SetDataList(3, stats);
            ScheduleSolve();
        }
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (_cloud != null)
                args.Display.DrawPointCloud(_cloud, 3);

            if (_outputContours.Count != 0 && Params.Output[2].Recipients.Count == 0)
                args.Display.DrawLines(_outputContours, Color.White, 1);
        }

        public override BoundingBox ClippingBox 
        {
            get
            {
                return _quadMesh.GetBoundingBox(false);
            }
        }

        protected override Bitmap Icon => Properties.Resources.Icons_Main;
        public override Guid ComponentGuid => new Guid("{53fefb98-1cec-4134-b707-0c366072af2c}");
        public override void AddedToDocument(GH_Document document)
        {
            if (Params.Input[0].SourceCount == 0)
            {
                List<IGH_DocumentObject> componentList = new List<IGH_DocumentObject>();
                PointF pivot;
                pivot = Attributes.Pivot;

                var calibrate = new Grasshopper.Kernel.Special.GH_ButtonObject();
                calibrate.CreateAttributes();
                calibrate.NickName = "calibrate";
                calibrate.Attributes.Pivot = new PointF(pivot.X - 250, pivot.Y - 46);
                calibrate.Attributes.ExpireLayout();
                calibrate.Attributes.PerformLayout();
                componentList.Add(calibrate);

                Params.Input[0].AddSource(calibrate);

                var reset = new Grasshopper.Kernel.Special.GH_ButtonObject();
                reset.CreateAttributes();
                reset.NickName = "reset";
                reset.Attributes.Pivot = new PointF(pivot.X - 250, pivot.Y - 21);
                reset.Attributes.ExpireLayout();
                reset.Attributes.PerformLayout();
                componentList.Add(reset);

                Params.Input[1].AddSource(reset);


                foreach (var component in componentList)
                    document.AddObject(component, false);


                document.UndoUtil.RecordAddObjectEvent("Add buttons", componentList);
            }

        }
        protected void ScheduleSolve()
        {
            OnPingDocument().ScheduleSolution(GeneralHelpers.ConvertFPStoMilliseconds(_refreshRate.Value), ScheduleDelegate);
        }
        protected void ScheduleDelegate(GH_Document doc)
        {
            ExpireSolution(false);
        }
    }
}