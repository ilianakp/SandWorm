using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;
using Microsoft.Azure.Kinect.Sensor;

using SandWorm.Analytics;
using static SandWorm.Core;
using static SandWorm.Structs;
using static SandWorm.SandWormComponentUI;
using System.Threading.Tasks;

namespace SandWorm
{
    public class SandWormComponent : GH_ExtendableComponent
    {
        #region Class Variables

        // Units & dimensions
        public static double unitsMultiplier = 1;

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
        private Mesh quadMesh;
        private PointCloud pointCloud;
        private List<Color> colorPalettes;
        private List<Rhino.Display.Text3d> labels;
        private Mesh waterMesh;

        private readonly LinkedList<int[]> renderBuffer = new LinkedList<int[]>();
        private int[] runningSum;
        private Vector2[] trimmedXYLookupTable = null;
        private Vector3?[] trimmedBooleanMatrix;
        private Color[] trimmedRGBArray;

        // Cut & Fill analysis
        private double?[] baseMeshElevationPoints;
        private Mesh baseMesh;
        private Rhino.Display.DisplayMaterial meshMaterial;
        private Color meshColor = Color.FromArgb(128, 128, 128);

        // Water flow analysis
        private Color blueWaterSurface = Color.FromArgb(75, 190, 255);
        private Color blueFlowLines = Color.FromArgb(75, 170, 255);
        private Rhino.Display.DisplayMaterial material;
        private ConcurrentDictionary<int, FlowLine> flowLines;
        Color[] waterColors = null;

        // Temporary storage of prior values for hotkey toggling
        public Dictionary<int, DateTime> _lastPressForKey = new Dictionary<int, DateTime>() {
            { (int)Keys.ShiftKey, DateTime.Now } 
        };
        public double _lastContourInterval;
        public double _lastWaterLevel;

        // Outputs
        private List<GeometryBase> _outputWaterSurface;
        private List<Line> _outputContours;

        // Debugging
        public static List<string> stats;
        protected Stopwatch timer;

        public bool reset;

        #endregion

        public SandWormComponent()
          : base("Sandworm Mesh", "SW Mesh",
            "Visualise Kinect depth data as a mesh", "SandWorm", "Visualisation")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Reset", "reset", "Re-initialise the camera stream and its processing. Use if your output is strange or behaving unexpectedly. ", GH_ParamAccess.item, reset);
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
            pManager.AddNumberParameter("Slopes", "Slopes", "", GH_ParamAccess.list);
            pManager.AddPointParameter("Verts", "Verts", "", GH_ParamAccess.list);
        }

        protected override void Setup(GH_ExtendableComponentAttributes attr) // Initialize the UI
        {
            MainComponentUI(attr);
            _lastContourInterval = _contourIntervalRange.Value;
            _lastWaterLevel = _waterLevel.Value;
        }

        protected override void OnComponentLoaded()
        {
            base.OnComponentLoaded();
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DA.GetData(0, ref reset);
            if (_calibrate.Active)
            {
                _sensorElevation.Value = Calibrate((KinectTypes)_sensorType.Value);
                ResetDataArrays();
            }

            if (reset || _reset)
            {

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
            _outputWaterSurface = new List<GeometryBase>();
            _outputContours = new List<Line>();

            if(material == null)
            {
                material = new Rhino.Display.DisplayMaterial(blueWaterSurface, blueWaterSurface, blueWaterSurface, blueWaterSurface, 0.7, 0.5);
                meshMaterial = new Rhino.Display.DisplayMaterial(meshColor, meshColor, meshColor, meshColor, 0.7, 0.5);
            }
                

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
                            _leftColumns.Value, _rightColumns.Value, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth);

                        Core.SetupCorrectiveLookupTables(KinectAzureController.idealXYCoordinates, KinectAzureController.verticalTiltCorrectionMatrix,
                            KinectAzureController.undistortMatrix, trimmedBooleanMatrix,
                            _leftColumns.Value, _rightColumns.Value, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth);
                        break;

                    case KinectTypes.KinectForWindows:
                        Core.TrimXYLookupTable(KinectForWindows.idealXYCoordinates, trimmedXYLookupTable,
                            _left, _right, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth);
                        break;
                }
            }


#if DEBUG
            GeneralHelpers.LogTiming(ref stats, timer, "Initial setup"); // Debug Info
#endif
            AverageAndBlurPixels(depthFrameDataInt, ref averagedDepthFrameData, runningSum, renderBuffer,
                                 _sensorElevation.Value, _averagedFrames.Value, _blurRadius.Value, trimmedWidth, trimmedHeight);

            GeneratePointCloud(averagedDepthFrameData, trimmedXYLookupTable,
                               KinectAzureController.verticalTiltCorrectionMatrix, (KinectTypes)_sensorType.Value,
                               allPoints, renderBuffer, trimmedWidth, trimmedHeight, _sensorElevation.Value, _averagedFrames.Value);

            #region RGB from camera
            if ((AnalysisTypes)_analysisType.Value == AnalysisTypes.Camera)
            {
                trimmedRGBArray = new Color[trimmedHeight * trimmedWidth];
                TrimColorArray(rgbArray, ref trimmedRGBArray, (KinectTypes)_sensorType.Value,
                    _left, _right, _bottomRows.Value, _topRows.Value, activeHeight, activeWidth);
            }
            #endregion

            #region Cut Fill
            // Calculate elevation points from mesh provided for Cut & Fill analysis. Only do this on reset.
            DA.GetData(2, ref baseMesh);
            if ((AnalysisTypes)_analysisType.Value == AnalysisTypes.CutFill && baseMeshElevationPoints == null)
                baseMeshElevationPoints = CutFill.MeshToPointArray(baseMesh, allPoints);
            #endregion

            colorPalettes = new List<Color>();
            DA.GetDataList(1, colorPalettes);

            List<Point3d> VertsOut = new List<Point3d>();

            List<double> valuesOut = GenerateMeshColors(ref _vertexColors, (AnalysisTypes)_analysisType.Value, averagedDepthFrameData,
                                                       trimmedXYLookupTable, trimmedRGBArray, _colorGradientRange.Value,
                                                       (Structs.ColorPalettes)_colorPalette.Value, colorPalettes, baseMeshElevationPoints,
                                                       allPoints, ref stats, _sensorElevation.Value, trimmedWidth, trimmedHeight, out VertsOut);
            DA.SetDataList(4, valuesOut);
            DA.SetDataList(5, VertsOut);

#if DEBUG
            GeneralHelpers.LogTiming(ref stats, timer, "Point cloud analysis");
#endif

            #region Contour lines
            if (_contourIntervalRange.Value > 0)
            {
                ContoursFromPoints.GetGeometryForAnalysis(ref _outputContours, allPoints,
                                                          (int)_contourIntervalRange.Value, trimmedWidth,
                                                          trimmedHeight, (int)_contourRoughness.Value);
                if (Params.Output[2].Recipients.Count > 0)
                {
                    Grasshopper.Kernel.Types.GH_Line[] ghLines = GeneralHelpers.ConvertLineToGHLine(_outputContours);
                    DA.SetDataList(2, ghLines);
                }
            }
#if DEBUG
            GeneralHelpers.LogTiming(ref stats, timer, "Contour lines");
#endif
            #endregion

            #region Labels
            if (_labelSpacing.Value > 0)
            {
                labels = new List<Rhino.Display.Text3d>();
                GeneralHelpers.CreateLabels(allPoints, ref labels, (AnalysisTypes)_analysisType.Value,
                                            baseMeshElevationPoints, trimmedWidth, trimmedHeight, (int)_labelSpacing.Value);
            }
#if DEBUG
            GeneralHelpers.LogTiming(ref stats, timer, "Labels");
#endif
            #endregion

            #region Flow lines
            if (_flowLinesLength.Value > 0)
            {
                if (flowLines == null)
                    flowLines = new ConcurrentDictionary<int, FlowLine>();

                FlowLine.DistributeRandomRaindrops(ref allPoints, ref flowLines, (int)_raindropSpacing.Value);
                FlowLine.GrowAndRemoveFlowlines(allPoints, flowLines, trimmedWidth, _flowLinesLength.Value);
            } else
                    flowLines = null;
#if DEBUG
            GeneralHelpers.LogTiming(ref stats, timer, "Flow lines");
#endif
            #endregion


            if ((OutputTypes)_outputType.Value == OutputTypes.Mesh)
            {
                pointCloud = null;
                quadMesh = CreateQuadMesh(ref quadMesh, allPoints, ref _vertexColors, ref trimmedBooleanMatrix, (KinectTypes)_sensorType.Value, trimmedWidth, trimmedHeight);
                DA.SetDataList(0, new List<Mesh> { quadMesh });

//#if DEBUG
                GeneralHelpers.LogTiming(ref stats, timer, "Meshing"); // Debug Info
//#endif
                if (_simulateFloodEvent.Active)
                {
                    MeshFlow.CalculateWaterHeadArray(allPoints, averagedDepthFrameData, trimmedWidth, trimmedHeight, _makeItRain.Active);
                    _makeItRain.Active = false;
                    GeneralHelpers.LogTiming(ref stats, timer, "Mesh Flow");
                    waterMesh = CreateQuadMesh(ref waterMesh, MeshFlow.waterElevationPoints, ref waterColors, ref trimmedBooleanMatrix, (KinectTypes)_sensorType.Value, trimmedWidth, trimmedHeight);

                }
            }
            else if ((OutputTypes)_outputType.Value == OutputTypes.PointCloud)
            {
                pointCloud = new PointCloud();

                if (_vertexColors.Length > 0)
                    pointCloud.AddRange(allPoints, _vertexColors);
                else
                    pointCloud.AddRange(allPoints);
#if DEBUG
                GeneralHelpers.LogTiming(ref stats, timer, "Point cloud display"); // Debug Info
#endif
            }

            if (_waterLevel.Value > 0)
            {
                WaterLevel.GetGeometryForAnalysis(ref _outputWaterSurface, _waterLevel.Value, quadMesh);
                if (Params.Output[1].Recipients.Count > 0)
                    DA.SetDataList(1, _outputWaterSurface);
            }

            DA.SetDataList(3, stats);
            ScheduleSolve();
        }
        public override void DrawViewportMeshes(IGH_PreviewArgs args)
        {
            if ((OutputTypes)_outputType.Value == OutputTypes.Mesh && quadMesh != null)
            {
                if ((AnalysisTypes)_analysisType.Value != AnalysisTypes.None)
                    args.Display.DrawMeshFalseColors(quadMesh);
                else
                    args.Display.DrawMeshShaded(quadMesh, meshMaterial);
            }
                

            if (_waterLevel.Value > 0 && _outputWaterSurface.Count > 0 && Params.Output[1].Recipients.Count == 0)
                args.Display.DrawMeshShaded((Mesh)_outputWaterSurface[0], material);
            
            if (_simulateFloodEvent.Active && waterMesh != null)
                args.Display.DrawMeshShaded(waterMesh, material);
        }
        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (pointCloud != null)
                args.Display.DrawPointCloud(pointCloud, 3);

            var grayscaled = (int)(_labelBrightness.Value * 12.75); // 0-20 into 0-255
            var labelAndContourColor = Color.FromArgb(grayscaled, grayscaled, grayscaled);

            if (_outputContours != null && _outputContours.Count != 0 && Params.Output[2].Recipients.Count == 0)
                args.Display.DrawLines(_outputContours, labelAndContourColor, 1);

            if (_labelSpacing.Value > 0 && ((AnalysisTypes)_analysisType.Value == AnalysisTypes.CutFill || (AnalysisTypes)_analysisType.Value == AnalysisTypes.Elevation))
            {
                if (labels != null)
                    foreach (var text in labels)
                        args.Display.Draw3dText(text, labelAndContourColor);
            }

            if (_flowLinesLength.Value > 0)
            {
                foreach (var kvp in flowLines)
                    args.Display.DrawPolyline(kvp.Value.Polyline, blueFlowLines);
            }
        }
        public override BoundingBox ClippingBox
        {
            get
            {
                if (quadMesh != null)
                    return quadMesh.GetBoundingBox(false);
                else
                    return new BoundingBox();
            }
        }
        protected override Bitmap Icon => Properties.Resources.Icons_Main;
        public override Guid ComponentGuid => new Guid("{53fefb98-1cec-4134-b707-0c366072af2c}");
        public override void AddedToDocument(GH_Document document)
        {
            Rhino.RhinoApp.KeyboardEvent -= new Rhino.RhinoApp.KeyboardHookEvent(SandwormShortcutHandler);
            Rhino.RhinoApp.KeyboardEvent += new Rhino.RhinoApp.KeyboardHookEvent(SandwormShortcutHandler);

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

        private void SandwormShortcutHandler(int keyIndex)
        {
            if (keyIndex == (int)Keys.ShiftKey)
            {
                _lastPressForKey[(int)Keys.ShiftKey] = DateTime.Now; // Track last SHIFT press to enable chord shortcuts
                return;  // Don't process SHIFT-activations on their own
            }

            // This handler has no access to events, so need to track time since last key event manually (to enable deduplication)
            if (_lastPressForKey.ContainsKey(keyIndex)) 
            {
                TimeSpan timeSinceLastPress = DateTime.Now - _lastPressForKey[keyIndex];
                if (timeSinceLastPress.TotalMilliseconds < 300) // Repeats must be 0.3s or more apart
                {
                    return; // Prevent retriggers within short time frames (e.g. keydown > keypress > keyrepeat > keyup)
                }
            }

            TimeSpan timeSinceShiftModifier = DateTime.Now - _lastPressForKey[(int)Keys.ShiftKey];
            if (timeSinceShiftModifier.TotalMilliseconds > 2000) // 2 seconds to activate F-key after SHIFT engaged
            {
                return; // Only activate F1-12 keys if SHIFT is used as a modifier chord
            }

            _lastPressForKey[keyIndex] = DateTime.Now; // Track activate for debouncing

            switch (keyIndex)
            {
                case (int)Keys.F2: // Start here as shift+F1 is an accessibility shortcut
                    _analysisType.Value = (int)Structs.AnalysisTypes.None;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to deactivate any analysis.");
                    break;

                case (int)Keys.F3:
                    _analysisType.Value = (int)Structs.AnalysisTypes.Camera;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to analyse the camera feed.");
                    break;

                case (int)Keys.F4:
                    _analysisType.Value = (int)Structs.AnalysisTypes.Elevation;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to analyse elevation.");
                    break;

                case (int)Keys.F5:
                    _analysisType.Value = (int)Structs.AnalysisTypes.Slope;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to analyse slope.");
                    break;

                case (int)Keys.F6:
                    _analysisType.Value = (int)Structs.AnalysisTypes.Aspect;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to analyse aspect.");
                    break;

                case (int)Keys.F7:
                    _analysisType.Value = (int)Structs.AnalysisTypes.CutFill;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to analyse cut/fill.");
                    break;

                case (int)Keys.F8: // Toggle contours on or off.
                    if (_contourIntervalRange.Value == 0)
                    {
                        _contourIntervalRange.Value = _lastContourInterval;
                    }
                    else
                    {
                        _lastContourInterval = _contourIntervalRange.Value;
                        _contourIntervalRange.Value = 0;
                    }
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to toggle contours.");
                    break;

                case (int)Keys.F9: // Toggle water plane on or off. 
                    if (_waterLevel.Value == 0)
                    {
                        _waterLevel.Value = _lastWaterLevel; 
                    }
                    else
                    {
                        _lastWaterLevel = _waterLevel.Value;
                        _waterLevel.Value = 0;
                    }
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to toggle water plane.");
                    break;

                case (int)Keys.F10: // Toggle flood sim 
                    _simulateFloodEvent.Active = !_simulateFloodEvent.Active;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to toggle flood simulation.");
                    break;

                case (int)Keys.F11: // Toggle flow sim
                    _makeItRain.Active = !_makeItRain.Active;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to toggle flow simulation.");
                    break;

                case (int)Keys.F12: // Reset camera
                    reset = true;
                    Rhino.RhinoApp.WriteLine("Activating Sandworm " + (Keys)keyIndex + " shortcut to reset camera.");
                    break;

                default:
                    break;
            }
        }

        private void ResetDataArrays()
        {
            quadMesh = null;
            trimmedXYLookupTable = null;
            runningSum = null;
            renderBuffer.Clear();
            baseMeshElevationPoints = null;
            flowLines = null;
            waterMesh = null;
            MeshFlow.waterHead = null;

            if (MeshFlow.accelerator != null)
            {
                MeshFlow.accelerator.Dispose();
                MeshFlow.context.Dispose();
            }
            

            _calibrate.Active = false; // Reset the button
            _resize = false;
            _reset = false;
        }
    }
}