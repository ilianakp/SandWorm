using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Kinect.Sensor;
using Rhino.Geometry;
using SandWorm.Analytics;

namespace SandWorm
{
    public static class Core
    {
        public static Mesh CreateQuadMesh(Mesh mesh, Point3d[] vertices, Color[] colors, Vector3?[] booleanMatrix, 
            Structs.KinectTypes kinectType, int xStride, int yStride)
        {        
            int xd = xStride;       // The x-dimension of the data
            int yd = yStride;       // They y-dimension of the data

            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.Vertices.Capacity = vertices.Length;      // Don't resize array
                mesh.Vertices.UseDoublePrecisionVertices = true;
                mesh.Vertices.AddVertices(vertices);

                switch (kinectType)
                {
                    case Structs.KinectTypes.KinectAzureNear:
                    case Structs.KinectTypes.KinectAzureWide:
                        for (int y = 1; y < yd - 1; y++)       // Iterate over y dimension
                            for (int x = 1; x < xd - 1; x++)       // Iterate over x dimension
                            {
                                int i = y * xd + x;
                                int j = (y - 1) * xd + x;

                                // This check is necessary for Kinect Azure WFOV
                                if (booleanMatrix[i] != null && booleanMatrix[i - 1] != null && booleanMatrix[j] != null && booleanMatrix[j - 1] != null)
                                    mesh.Faces.AddFace(j - 1, j, i, i - 1);
                            }
                        break;

                    case Structs.KinectTypes.KinectForWindows:
                        for (int y = 1; y < yd - 1; y++)       // Iterate over y dimension
                            for (int x = 1; x < xd - 1; x++)       // Iterate over x dimension
                            {
                                int i = y * xd + x;
                                int j = (y - 1) * xd + x;
                                mesh.Faces.AddFace(j - 1, j, i, i - 1);
                            }
                        break;
                }


            }
            else
            {
                unsafe
                {
                    using (var meshAccess = mesh.GetUnsafeLock(true))
                    {
                        int arrayLength;
                        Point3d* points = meshAccess.VertexPoint3dArray(out arrayLength);
                        for (int i = 0; i < arrayLength; i++)
                        {
                            points->Z = vertices[i].Z;
                            points++;
                        }
                        mesh.ReleaseUnsafeLock(meshAccess);
                    }
                }
            }

            if (colors.Length > 0) // Colors only provided if the mesh style permits
                mesh.VertexColors.SetColors(colors);
            else
                mesh.VertexColors.Clear();

            return mesh;
        }


        public static void GetTrimmedDimensions(Structs.KinectTypes kinectType, ref int trimmedWidth, ref int trimmedHeight, int[] runningSum,
                                                double topRows, double bottomRows, double leftColumns, double rightColumns)
        {
            int _x;
            int _y;
            switch (kinectType)
            {
                case Structs.KinectTypes.KinectForWindows:
                    _x = KinectForWindows.depthWidth;
                    _y = KinectForWindows.depthHeight;
                    break;
                case Structs.KinectTypes.KinectAzureNear:
                    _x = KinectAzureController.depthWidthNear;
                    _y = KinectAzureController.depthHeightNear;
                    break;
                case Structs.KinectTypes.KinectAzureWide:
                    _x = KinectAzureController.depthWidthWide;
                    _y = KinectAzureController.depthHeightWide;
                    break;
                default:
                    throw new ArgumentException("Invalid Kinect Type", "original");
            }

            runningSum = Enumerable.Range(1, _x * _y).Select(i => new int()).ToArray();

            trimmedWidth = (int)(_x - leftColumns - rightColumns);
            trimmedHeight = (int)(_y - topRows - bottomRows);
        }

        public static void SetupCorrectiveLookupTables(Vector2[] idealXYCoordinates, double[] verticalTiltCorrectionLookupTable,
            Vector3?[] booleanMatrix, Vector3?[] trimmedBooleanMatrix,
            double leftColumns, double rightColumns, double topRows, double bottomRows, int height, int width) 
        {
            ref Vector3? bv0 = ref booleanMatrix[0];
            ref Vector3? bd0 = ref trimmedBooleanMatrix[0];

            int _yStride = height - (int)bottomRows;
            int _xStride = width - (int)leftColumns;

            for (int rows = (int)topRows, j = 0; rows < _yStride; rows++)
            {
                for (int columns = (int)rightColumns; columns < _xStride; columns++, j++)
                {
                    int i = rows * width + columns;
                    verticalTiltCorrectionLookupTable[j] = idealXYCoordinates[i].Y * KinectAzureController.sin6;

                    Unsafe.Add(ref bd0, j) = Unsafe.Add(ref bv0, i);
                }
            }
        }

        public static void TrimXYLookupTable(Vector2[] sourceXY, Vector2[] destinationXY,
            double leftColumns, double rightColumns, double topRows, double bottomRows, int height, int width, double unitsMultiplier) 
        {
            ref Vector2 rv0 = ref sourceXY[0];
            ref Vector2 rd0 = ref destinationXY[0];

            int _yStride = height - (int)bottomRows;
            int _xStride = width - (int)leftColumns;
            float _units = (float)unitsMultiplier;

            for (int rows = (int)topRows, j = 0; rows < _yStride; rows++)
            {
                for (int columns = (int)rightColumns; columns < _xStride; columns++, j++)
                {
                    int i = rows * width + columns;
                    Unsafe.Add(ref rd0, j).X = Unsafe.Add(ref rv0, i).X * _units;
                    Unsafe.Add(ref rd0, j).Y = Unsafe.Add(ref rv0, i).Y * _units;
                }
            }
        }


        public static void SetupRenderBuffer(int[] depthFrameDataInt, Structs.KinectTypes kinectType, ref BGRA[] rgbArray, int analysisType,
            double leftColumns, double rightColumns, double topRows, double bottomRows, double sensorElevation, ref int activeHeight, ref int activeWidth,
            double averageFrames, int[] runningSum, LinkedList<int[]> renderBuffer)
        {

            ushort[] depthFrameData;

            if (kinectType == Structs.KinectTypes.KinectForWindows)
            {
                KinectForWindows.SetupSensor();
                depthFrameData = KinectForWindows.depthFrameData;
                activeHeight = KinectForWindows.depthHeight;
                activeWidth = KinectForWindows.depthWidth;
            }
            else
            {
                KinectAzureController.SetupSensor(kinectType, sensorElevation);
                KinectAzureController.CaptureFrame(ref rgbArray, analysisType);
                depthFrameData = KinectAzureController.depthFrameData;
                activeHeight = KinectAzureController.depthHeight;
                activeWidth = KinectAzureController.depthWidth;
            }

            // Trim the depth array and cast ushort values to int 
            CopyAsIntArray(depthFrameData, depthFrameDataInt,
                leftColumns, rightColumns, topRows, bottomRows,
                activeHeight, activeWidth);

            // Reset everything when changing the amount of frames to average across
            if (renderBuffer.Count > averageFrames)
            {
                renderBuffer.Clear();
                Array.Clear(runningSum, 0, runningSum.Length);
                renderBuffer.AddLast(depthFrameDataInt);
            }
            else
            {
                renderBuffer.AddLast(depthFrameDataInt);
            }

        }

        public static void AverageAndBlurPixels(int[] depthFrameDataInt, ref double[] averagedDepthFrameData, int[] runningSum, LinkedList<int[]> renderBuffer,
            double sensorElevation, double averageFrames,
            double blurRadius, int trimmedWidth, int trimmedHeight)
        {
            // Average across multiple frames
            for (var pixel = 0; pixel < depthFrameDataInt.Length; pixel++)
            {
                if (depthFrameDataInt[pixel] > 200) // We have a valid pixel. 
                {
                    runningSum[pixel] += depthFrameDataInt[pixel];
                }
                else
                {
                    if (pixel > 0) // Pixel is invalid and we have a neighbor to steal information from
                    {
                        //D1 Method
                        runningSum[pixel] += depthFrameDataInt[pixel - 1];

                        // Replace the zero value from the depth array with the one from the neighboring pixel
                        renderBuffer.Last.Value[pixel] = depthFrameDataInt[pixel - 1];
                    }
                    else // Pixel is invalid and it is the first one in the list. (No neighbor on the left hand side, so we set it to the lowest point on the table)
                    {
                        runningSum[pixel] += (int)sensorElevation;
                        renderBuffer.Last.Value[pixel] = (int)sensorElevation;
                    }
                }

                averagedDepthFrameData[pixel] = runningSum[pixel] / renderBuffer.Count; // Calculate average values
                
                if (renderBuffer.Count >= averageFrames)
                    runningSum[pixel] -= renderBuffer.First.Value[pixel]; // Subtract the oldest value from the sum 
            }


            if (blurRadius > 1) // Apply gaussian blur
            {
                var gaussianBlurProcessor = new GaussianBlurProcessor((int)blurRadius, trimmedWidth, trimmedHeight);
                gaussianBlurProcessor.Apply(averagedDepthFrameData);
            }
        }

        public static void GeneratePointCloud(double[] averagedDepthFrameData, Vector2[] trimmedXYLookupTable, double[] verticalTiltCorrectionLookupTable, Structs.KinectTypes kinectType,
            Point3d[] allPoints, LinkedList<int[]> renderBuffer, int trimmedWidth, int trimmedHeight, double sensorElevation, double unitsMultiplier, double averageFrames)
        {
            Point3d tempPoint = new Point3d();

            switch (kinectType)
            {
                case Structs.KinectTypes.KinectAzureNear:
                case Structs.KinectTypes.KinectAzureWide:

                    double correctedElevation = 0.0;
                    for (int rows = 0, i = 0; rows < trimmedHeight; rows++)
                        for (int columns = 0; columns < trimmedWidth; columns++, i++)
                        {
                            tempPoint.X = trimmedXYLookupTable[i].X * -1;
                            tempPoint.Y = trimmedXYLookupTable[i].Y;

                            // Correct for Kinect Azure's tilt of the depth camera
                            correctedElevation = averagedDepthFrameData[i] - verticalTiltCorrectionLookupTable[i];
                            tempPoint.Z = (correctedElevation - sensorElevation) * -unitsMultiplier;
                            averagedDepthFrameData[i] = correctedElevation;

                            allPoints[i] = tempPoint;
                        }
                    break;

                case Structs.KinectTypes.KinectForWindows:
                    for (int rows = 0, i = 0; rows < trimmedHeight; rows++)
                        for (int columns = 0; columns < trimmedWidth; columns++, i++)
                        {
                            tempPoint.X = trimmedXYLookupTable[i].X;
                            tempPoint.Y = trimmedXYLookupTable[i].Y * -1;
                            tempPoint.Z = (averagedDepthFrameData[i] - sensorElevation) * -unitsMultiplier;
                            allPoints[i] = tempPoint;
                        }
                    break;

            }



            // Keep only the desired amount of frames in the buffer
            while (renderBuffer.Count >= averageFrames) renderBuffer.RemoveFirst();
        }


        public static void GenerateMeshColors(ref Color[] vertexColors, Structs.AnalysisTypes analysisType, double[] averagedDepthFrameData, 
            Vector2[]xyLookuptable, Color[] pixelColors, double gradientRange, Structs.ColorPalettes colorPalette, List<Color> customColors,
            double?[] baseMeshElevationPoints, Point3d[] allPoints, ref List<string> stats,
            double sensorElevation, int trimmedWidth, int trimmedHeight, double unitsMultiplier)
        {
            switch (analysisType)
            {
                case Structs.AnalysisTypes.None:
                    vertexColors = new None().GetColorCloudForAnalysis();
                    break;

                case Structs.AnalysisTypes.Camera:
                    vertexColors = pixelColors;
                    break;

                case Structs.AnalysisTypes.Elevation:
                    vertexColors = new Elevation().GetColorCloudForAnalysis(averagedDepthFrameData, sensorElevation, gradientRange, colorPalette, customColors);
                    break;

                case Structs.AnalysisTypes.Slope:
                    vertexColors = new Slope().GetColorCloudForAnalysis(averagedDepthFrameData,
                        trimmedWidth, trimmedHeight, gradientRange, xyLookuptable);
                    break;

                case Structs.AnalysisTypes.Aspect:
                    vertexColors = new Aspect().GetColorCloudForAnalysis(averagedDepthFrameData,
                        trimmedWidth, trimmedHeight, gradientRange);
                    break;

                case Structs.AnalysisTypes.CutFill:
                    vertexColors = new CutFill().GetColorCloudForAnalysis(allPoints, baseMeshElevationPoints, gradientRange, colorPalette, customColors, ref stats, unitsMultiplier);
                    break;
            }
        }

        public static void CopyAsIntArray(ushort[] source, int[] destination, 
            double leftColumns, double rightColumns, double topRows, double bottomRows, int height, int width)
        {
            if (source == null)
                return; // Triggers on initial setup

            ref ushort ru0 = ref source[0];
            ref int ri0 = ref destination[0];

            for (int rows = (int)topRows, j = 0; rows < height - bottomRows; rows++)
            {
                for (int columns = (int)rightColumns; columns < width - leftColumns; columns++, j++)
                {
                    int i = rows * width + columns;
                    Unsafe.Add(ref ri0, j) = Unsafe.Add(ref ru0, i);
                }
            }
        }

        public static void TrimColorArray(BGRA[] source, ref Color[] destination, Structs.KinectTypes kinectType,
            double leftColumns, double rightColumns, double topRows, double bottomRows, int height, int width) 
        {
            int _yStride = height - (int)bottomRows;
            int _xStride = width - (int)leftColumns;

            switch (kinectType)
            {
                case Structs.KinectTypes.KinectAzureNear:
                case Structs.KinectTypes.KinectAzureWide:

                    for (int rows = (int)topRows, j = 0; rows < _yStride; rows++)
                    {
                        for (int columns = (int)rightColumns; columns < _xStride; columns++, j++)
                        {
                            int i = rows * width + columns;
                            destination[j] = Color.FromArgb(source[i].R, source[i].G, source[i].B);
                        }
                    }
                    break;

                case Structs.KinectTypes.KinectForWindows:
                    if (KinectForWindows.rgbColorData == null)
                        break;
                    for (int rows = (int)topRows, j = 0; rows < _yStride; rows++)
                    {
                        for (int columns = (int)rightColumns; columns < _xStride; columns++, j++)
                        {
                            int i = rows * width + columns;
                            destination[j] = KinectForWindows.rgbColorData[i];
                        }
                    }
                    break;
            }
        }

        public static double Calibrate(Structs.KinectTypes kinectType)
        {
            switch(kinectType)
            {
                case Structs.KinectTypes.KinectAzureNear:
                case Structs.KinectTypes.KinectAzureWide:
                    return KinectAzureController.Calibrate(kinectType);

                case Structs.KinectTypes.KinectForWindows:
                    return KinectForWindows.Calibrate();

                default:
                    return 0;
            }
        }
    }
}