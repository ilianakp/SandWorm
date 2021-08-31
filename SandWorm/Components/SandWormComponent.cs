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
using static SandWorm.Structs;
using static SandWorm.SandWormComponentUI;
using Microsoft.Azure.Kinect.Sensor;

namespace SandWorm
{
    public class SandWormComponent : GH_ExtendableComponent
    {
        #region Class Variables

        // Units & dimensions
        private double unitsMultiplier = 1;

        private int activeHeight = 0;
        private int activeWidth = 0;
        private int trimmedHeight;
        private int trimmedWidth;

        private double _left;
        private double _right;

        // Data arrays
        private BGRA[] rgbArray;
        private Point3d[] allPoints;
        private Color[] _vertexColors;
        private Mesh _quadMesh;
        private PointCloud _cloud;
        private List<Color> colorPalettes;
        private List<Rhino.Display.Text3d> labels;

        private readonly LinkedList<int[]> renderBuffer = new LinkedList<int[]>();
        private int[] runningSum;
        private Vector2[] trimmedXYLookupTable = null;
        private Vector3?[] trimmedBooleanMatrix;
        private Color[] trimmedRGBArray;

        // Cut & Fill analysis
        private double?[] baseMeshElevationPoints;
        private Mesh baseMesh;

        // Water flow analysis
        private int flowLinesAge = 0;
        private List<FlowLine> flowLines;

        // Outputs
        private List<Mesh> _outputMesh;
        private List<GeometryBase> _outputWaterSurface;
        private List<Line> _outputContours;

        // Debugging
        public static List<string> stats;
        protected Stopwatch timer;

        // Boolean controls
        public bool reset;

        #endregion

        public SandWormComponent()
          : base("Sandworm Mesh", "SW Mesh",
            "Visualise Kinect depth data as a mesh", "SandWorm", "Visualisation")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Reset", "reset", "Hitting this button will reset everything to defaults.", GH_ParamAccess.item, reset);
            pManager.AddColourParameter("Color list", "color list", "Provide a list of custom colors to define a gradient for the elevation analysis.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Mesh", "mesh", "Provide a base mesh for the Cut & Fill analysis mode.", GH_ParamAccess.item);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
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
            DA.GetData(0, ref reset);
            if (reset || _reset)
            {
                if (_calibrate.Active)
                    _sensorElevation.Value = Calibrate((KinectTypes)_sensorType.Value);

                if ((KinectTypes)_sensorType.Value == KinectTypes.KinectForWindows)
                    KinectForWindows.RemoveRef();
                else 
                    KinectAzureController.RemoveRef();

                ResetDataArrays();
                unitsMultiplier = GeneralHelpers.ConvertDrawingUnits(RhinoDoc.ActiveDoc.ModelUnitSystem);
            }

            if (_resize)
            {
                ResetDataArrays();
            }

            // Flip left and right columns for Kinect for Windows
            GeneralHelpers.SwapLeftRight((KinectTypes)_sensorType.Value, _leftColumns.Value, _rightColumns.Value, ref _left, ref _right);
            GeneralHelpers.SetupLogging(ref timer, ref stats);

            // Trim 
            GetTrimmedDimensions((KinectTypes)_sensorType.Value, ref trimmedWidth, ref trimmedHeight, runningSum,
                                  _bottomRows.Value, _topRows.Value, _left, _right);


            // Initialize
            int[] depthFrameDataInt = new int[trimmedWidth * trimmedHeight];
            double[] averagedDepthFrameData = new double[trimmedWidth * trimmedHeight];
            allPoints = new Point3d[trimmedWidth * trimmedHeight];
            _outputMesh = new List<Mesh>();
            _outputWaterSurface = new List<GeometryBase>();
            _outputContours = new List<Line>();

            if (runningSum == null)
                runningSum = Enumerable.Range(1, depthFrameDataInt.Length).Select(i => new int()).ToArray();

            SetupRenderBuffer(depthFrameDataInt, (KinectTypes)_sensorType.Value, ref rgbArray, _analysisType.Value,
                              _left, _right, _bottomRows.Value, _topRows.Value, _sensorElevation.Value, ref activeHeight,
                              ref activeWidth, _averagedFrames.Value, runningSum, renderBuffer);


            if (trimmedXYLookupTable == null) // Only recalculate on reset
            {
                trimmedXYLookupTable = new Vector2[trimmedWidth * trimmedHeight];
                trimmedBooleanMatrix = new Vector3?[trimmedWidth * trimmedHeight];

                switch ((KinectTypes)_sensorType.Value)
                {
                    case KinectTypes.KinectAzureNear:
                    case KinectTypes.KinectAzureWide:
                        Core.TrimXYLookupTable(KinectAzureController.idealXYCoordinates, trimmedXYLookupTable,
                            _leftColumns.Value, _rightColumns.Value, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth, unitsMultiplier);

                        Core.SetupCorrectiveLookupTables(KinectAzureController.idealXYCoordinates, KinectAzureController.verticalTiltCorrectionMatrix,
                            KinectAzureController.undistortMatrix, trimmedBooleanMatrix,
                            _leftColumns.Value, _rightColumns.Value, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth);
                        break;

                    case KinectTypes.KinectForWindows:
                        Core.TrimXYLookupTable(KinectForWindows.idealXYCoordinates, trimmedXYLookupTable,
                            _left, _right, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth, unitsMultiplier);
                        break;
                }
            }

            GeneralHelpers.LogTiming(ref stats, timer, "Initial setup"); // Debug Info

            AverageAndBlurPixels(depthFrameDataInt, ref averagedDepthFrameData, runningSum, renderBuffer,
                                 _sensorElevation.Value, _averagedFrames.Value, _blurRadius.Value, trimmedWidth,
                                 trimmedHeight);

            GeneratePointCloud(averagedDepthFrameData, trimmedXYLookupTable,
                               KinectAzureController.verticalTiltCorrectionMatrix, (KinectTypes)_sensorType.Value,
                               allPoints, renderBuffer, trimmedWidth, trimmedHeight, _sensorElevation.Value,
                               unitsMultiplier, _averagedFrames.Value);

            // Produce 1st type of analysis that acts on the pixel array and assigns vertex colors
            if ((AnalysisTypes)_analysisType.Value == AnalysisTypes.Camera)
            {
                trimmedRGBArray = new Color[trimmedHeight * trimmedWidth];
                TrimColorArray(rgbArray, ref trimmedRGBArray, (KinectTypes)_sensorType.Value,
                    _left, _right, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth);
            }

            // Calculate elevation points from mesh provided for Cut & Fill analysis. Only do this on reset.
            DA.GetData(2, ref baseMesh);
            if ((AnalysisTypes)_analysisType.Value == AnalysisTypes.CutFill && baseMeshElevationPoints == null)
                baseMeshElevationPoints = CutFill.MeshToPointArray(baseMesh, allPoints);


            colorPalettes = new List<Color>();
            DA.GetDataList(1, colorPalettes);

            GenerateMeshColors(ref _vertexColors, (AnalysisTypes)_analysisType.Value, averagedDepthFrameData,
                               trimmedXYLookupTable, trimmedRGBArray, _colorGradientRange.Value,
                               (Structs.ColorPalettes)_colorPalette.Value, colorPalettes, baseMeshElevationPoints,
                               allPoints, ref stats, _sensorElevation.Value, trimmedWidth, trimmedHeight,
                               unitsMultiplier);

            if (_labelSpacing.Value > 0)
            {
                labels = new List<Rhino.Display.Text3d>();
                GeneralHelpers.CreateLabels(allPoints, ref labels, (AnalysisTypes)_analysisType.Value,
                                            baseMeshElevationPoints, trimmedWidth, trimmedHeight,
                                            (int)_labelSpacing.Value, unitsMultiplier);
            }

            if (_rainDensity.Value > 0)
            {
                if (flowLinesAge == 0)
                {
                    flowLines = new List<FlowLine>();
                    FlowLine.CreateFlowLines(allPoints, ref flowLines, trimmedWidth, trimmedHeight, (int)_rainDensity.Value);
                }
                if (flowLinesAge < 30)
                {
                    foreach (var _flowLine in flowLines)
                        _flowLine.Grow(ref allPoints, trimmedWidth);
                    flowLinesAge++;
                } 
                else if (flowLines[0].Polyline.Length > 0)
                {
                    foreach (var _flowLine in flowLines)
                        _flowLine.Shrink();
                }
                else
                {
                    flowLinesAge = 0;
                }
            }

            GeneralHelpers.LogTiming(ref stats, timer, "Point cloud analysis"); // Debug Info

            if ((OutputTypes)_outputType.Value == OutputTypes.Mesh)
            {
                _cloud = null;
                // Generate the mesh
                _quadMesh = CreateQuadMesh(_quadMesh, allPoints, _vertexColors, trimmedBooleanMatrix, (KinectTypes)_sensorType.Value, trimmedWidth, trimmedHeight);
                _outputMesh.Add(_quadMesh);

                GeneralHelpers.LogTiming(ref stats, timer, "Meshing"); // Debug Info

                // Produce 2nd type of analysis that acts on the mesh and creates new geometry
                if (_contourIntervalRange.Value > 0)
                {
                    ContoursFromPoints.GetGeometryForAnalysis(ref _outputContours, allPoints,
                                                              (int)_contourIntervalRange.Value, trimmedWidth,
                                                              trimmedHeight, (int)_contourRoughness.Value,
                                                              unitsMultiplier);
                    if (Params.Output[2].Recipients.Count > 0)
                    {
                        Grasshopper.Kernel.Types.GH_Line[] ghLines = GeneralHelpers.ConvertLineToGHLine(_outputContours);
                        DA.SetDataList(2, ghLines);
                    }
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
                WaterLevel.GetGeometryForAnalysis(ref _outputWaterSurface, _waterLevel.Value, _quadMesh);
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

            if (_outputContours != null && _outputContours.Count != 0 && Params.Output[2].Recipients.Count == 0)
                args.Display.DrawLines(_outputContours, Color.White, 1);
            
            if (_labelSpacing.Value > 0 && ((AnalysisTypes) _analysisType.Value == AnalysisTypes.CutFill || (AnalysisTypes)_analysisType.Value == AnalysisTypes.Elevation))
            {
                if (labels != null)
                    foreach (var text in labels)
                        args.Display.Draw3dText(text, Color.White);
            }

            if (_rainDensity.Value > 0)
            {
                foreach (var _flowLine in flowLines)
                    args.Display.DrawPolyline(_flowLine.Polyline, Color.Blue);
            }
            }

        public override BoundingBox ClippingBox 
        {
            get
            {
                if (_quadMesh != null)
                    return _quadMesh.GetBoundingBox(false);
                else
                    return new BoundingBox();
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

                var reset = new Grasshopper.Kernel.Special.GH_ButtonObject();
                reset.CreateAttributes();
                reset.NickName = "reset";
                reset.Attributes.Pivot = new PointF(pivot.X - 200, pivot.Y - 38);
                reset.Attributes.ExpireLayout();
                reset.Attributes.PerformLayout();
                componentList.Add(reset);

                Params.Input[0].AddSource(reset);


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

        private void ResetDataArrays()
        {
            _quadMesh = null;
            trimmedXYLookupTable = null;
            runningSum = null;
            renderBuffer.Clear();
            baseMeshElevationPoints = null;
            
            _calibrate.Active = false; // Untick the UI checkbox
            _resize = false;
            _reset = false;
        }
    }
}