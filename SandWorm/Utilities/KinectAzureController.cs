using System;
using System.Numerics;
using Microsoft.Azure.Kinect.Sensor;


namespace SandWorm
{

    static class KinectAzureController
    {
        // Shared across devices
        public static int depthHeight = 0;
        public static int depthWidth = 0;
        public static int colorHeight = 0;
        public static int colorWidth = 0;
        public static ushort[] depthFrameData = null;
        public static byte[] colorFrameData = null;
        public static Image colorForDepthImage = null;
        public static Vector2[] idealXYCoordinates;

        // Kinect for Azure specific
        public static Device sensor;
        private static DeviceConfiguration deviceConfig;
        private static Calibration calibration;
        private static Transformation transformation;

        public const int depthWidthNear = 640; // Assuming low FPS mode
        public const int depthHeightNear = 576;

        public const int depthWidthWide = 1024; // Assuming low FPS mode
        public const int depthHeightWide = 1024;

        public static Vector3?[] undistortMatrix;
        public static double[] verticalTiltCorrectionMatrix;

        public const double sin6 = 0.10452846326;


        public static DepthMode GetDepthMode(Structs.KinectTypes type)
        {
            switch (type)
            {
                case Structs.KinectTypes.KinectAzureNear:
                    depthWidth = depthWidthNear;
                    depthHeight = depthHeightNear;
                    return DepthMode.NFOV_Unbinned;

                case Structs.KinectTypes.KinectAzureWide:
                    depthWidth = depthWidthWide;
                    depthHeight = depthHeightWide;
                    return DepthMode.WFOV_Unbinned;

                default:
                    throw new ArgumentException("Invalid Kinect Type provided", "original"); ;
            }
        }

        public static double Calibrate(Structs.KinectTypes fieldOfViewMode)
        {
            CreateCameraConfig(fieldOfViewMode);

            int minX = 3 * (depthWidth / 2) - (3 * 10); // Multiply by 3 for each of the X,Y,Z coordinates
            int maxX = 3 * (depthWidth / 2) + (3 * 10); // Multiply by 3 for each of the X,Y,Z coordinates
            int minY = (depthHeight / 2) - 10;
            int maxY = (depthHeight / 2) + 10;

            double averagedSensorElevation = 0.0;
            int counter = 0;

            using (Capture capture = sensor.GetCapture())
            {
                if (capture.Depth == null)
                    return 0; // No depth data obtained 

                calibration = sensor.GetCalibration(deviceConfig.DepthMode, deviceConfig.ColorResolution);
                transformation = calibration.CreateTransformation();

                var pointCloud = transformation.DepthImageToPointCloud(capture.Depth);
                var pointCloudBuffer = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(pointCloud.Memory.Span); // Cast byte values to short

                for (int y = minY; y < maxY; y++)       // Iterate over y dimension
                {
                    for (int x = minX + 2; x < maxX; x += 3, counter++)       // Iterate over x dimension. Increment by 3 to skip over the X,Y coordinates
                    {
                        int i = y * depthWidth * 3 + x;
                        averagedSensorElevation += pointCloudBuffer[i];
                    }
                }
            }
            return Math.Round(averagedSensorElevation /= counter);
        }

        public static void RemoveRef()
        {
            sensor.Dispose();
            sensor = null;
        }

        private static void CreateCameraConfig(Structs.KinectTypes fieldOfViewMode)
        {
            deviceConfig = new DeviceConfiguration
            {
                CameraFPS = FPS.FPS15,
                DepthMode = GetDepthMode(fieldOfViewMode),
                ColorFormat = ImageFormat.ColorBGRA32,
                ColorResolution = ColorResolution.R1536p,
                SynchronizedImagesOnly = true 
            };

            if (fieldOfViewMode == Structs.KinectTypes.KinectAzureNear) // We can have 30 FPS in the narrow field of view
                deviceConfig.CameraFPS = FPS.FPS30;
        }

        // Capture a single frame
        public static void CaptureFrame(ref BGRA[] colorArray, int analysisType)
        {
            using (Capture capture = sensor.GetCapture())
            {
                if (capture.Depth != null)
                    depthFrameData = capture.Depth.GetPixels<ushort>().ToArray();

                if ((Structs.AnalysisTypes) analysisType == Structs.AnalysisTypes.Camera && capture.Color != null)
                {
                    Image rgb = null;
                    if (deviceConfig.DepthMode == DepthMode.NFOV_Unbinned)
                        rgb = new Image(ImageFormat.ColorBGRA32, depthWidthNear, depthHeightNear, 2560);
                    else
                        rgb = new Image(ImageFormat.ColorBGRA32, depthWidthWide, depthHeightWide, 4096);

                    transformation.ColorImageToDepthCamera(capture, rgb);
                    colorArray = rgb.GetPixels<BGRA>().ToArray();
                }
            }
        }


        public static void SetupSensor(Structs.KinectTypes fieldOfViewMode, double sensorElevation)
        {
            if (sensor == null)
            {
                string message;
                CreateCameraConfig(fieldOfViewMode); // Apply user options from Sandworm Options

                try
                {
                    sensor = Device.Open();
                    sensor.StopCameras();
                }
                catch (Exception)
                {
                }

                try
                {
                    sensor.StartCameras(deviceConfig);
                    calibration = sensor.GetCalibration(deviceConfig.DepthMode, deviceConfig.ColorResolution);
                    transformation = calibration.CreateTransformation();

                    Vector2 depthPixel = new Vector2();
                    Vector3? translationVector;

                    // Lookup tables to correct for depth camera distortion in XY plane and its vertical tilt
                    undistortMatrix = new Vector3?[depthHeight * depthWidth];
                    verticalTiltCorrectionMatrix = new double[depthHeight * depthWidth];

                    for (int y = 0, i = 0; y < depthHeight - 0; y++)
                    {
                        depthPixel.Y = (float)y;
                        for (int x = 0; x < depthWidth - 0; x++, i++)
                        {
                            depthPixel.X = (float)x;
                            
                            translationVector = calibration.TransformTo3D(depthPixel, 1f, CalibrationDeviceType.Depth, CalibrationDeviceType.Depth);
                            undistortMatrix[i] = translationVector;

                            if (translationVector != null)
                                verticalTiltCorrectionMatrix[i] = translationVector.Value.Y * sin6;
                        }
                    }

                    // Create synthetic depth values emulating our sensor elevation and obtain corresponding idealized XY coordinates
                    double syntheticDepthValue;
                    idealXYCoordinates = new Vector2[depthWidth * depthHeight];
                    for (int i = 0; i < depthWidth * depthHeight; i++)
                    {
                        if (undistortMatrix[i] != null)
                        {
                            syntheticDepthValue = sensorElevation / (1 - verticalTiltCorrectionMatrix[i]);
                            idealXYCoordinates[i] = new Vector2((float)Math.Round(syntheticDepthValue * undistortMatrix[i].Value.X, 1), (float)Math.Round(syntheticDepthValue * undistortMatrix[i].Value.Y, 1));
                        }
                        else
                            idealXYCoordinates[i] = new Vector2();
                    }
                }
                catch (Exception ex)
                {
                    message = ex.ToString();
                    //sensor?.Dispose();
                }
            }
        }
    }
}