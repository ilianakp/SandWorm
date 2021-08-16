using System;
using System.Numerics;
using System.Windows.Media;
using Microsoft.Kinect;


namespace SandWorm
{
    static class KinectForWindows
    {
        // Shared across devices
        public static int depthHeight = 424;
        public static int depthWidth = 512;
        public static int colorHeight = 0;
        public static int colorWidth = 0;
        public static ushort[] depthFrameData = null;
        private static byte[] colorFrameData = null;
        public static System.Drawing.Color[] rgbColorData = null;
        public static Vector2[] idealXYCoordinates;

        // Kinect for Windows specific
        public static KinectSensor sensor = null;
        public static MultiSourceFrameReader multiFrameReader = null;
        public static FrameDescription depthFrameDescription = null;
        public static FrameDescription colorFrameDescription = null;
        public static int refc = 0;
        public static int bytesForPixelColor = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        public const double kinect2FOVForX = 70.6;
        public const double kinect2FOVForY = 60.0;

        public static void AddRef()
        {
            if (sensor == null)
            {
                Initialize();
            }
            if (sensor != null)
            {
                refc++;
            }
        }

        public static void SetupSensor()
        {
            if (sensor == null)
            {
                AddRef();
                //sensor = KinectForWindows.sensor;
            }
        }

        public static void RemoveRef()
        {
            refc--;
            if ((sensor != null) && (refc == 0))
            {
                multiFrameReader.MultiSourceFrameArrived -= Reader_FrameArrived;
                sensor.Close();
                sensor = null;
            }
        }

        public static void Initialize()
        {
            
            sensor = KinectSensor.GetDefault();
            multiFrameReader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color);
            multiFrameReader.MultiSourceFrameArrived += new EventHandler<MultiSourceFrameArrivedEventArgs>(Reader_FrameArrived);
            ComputeXYCoordinates();

            sensor.Open();
        }

        private static void ComputeXYCoordinates()
        {
            PointF[] intrinsicCoordinates = sensor.CoordinateMapper.GetDepthFrameToCameraSpaceTable();
            idealXYCoordinates = new Vector2[depthWidth * depthHeight];
            Vector2 _point = new Vector2();

            for (int i = 0; i < intrinsicCoordinates.Length; i++)
            {
                _point.X = intrinsicCoordinates[i].X * (float)SandWormComponentUI._sensorElevation.Value;
                _point.Y = intrinsicCoordinates[i].Y * (float)SandWormComponentUI._sensorElevation.Value;
                idealXYCoordinates[i] = _point;
            }
        }

        private static void Reader_FrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            if (e.FrameReference != null)
            {
                MultiSourceFrame multiFrame = e.FrameReference.AcquireFrame();

                if (multiFrame.DepthFrameReference != null)
                {
                    try
                    {
                        using (DepthFrame depthFrame = multiFrame.DepthFrameReference.AcquireFrame())
                        {
                            if (depthFrame != null)
                            {
                                using (KinectBuffer buffer = depthFrame.LockImageBuffer())
                                {
                                    depthFrameDescription = depthFrame.FrameDescription;
                                    depthWidth = depthFrameDescription.Width;
                                    depthHeight = depthFrameDescription.Height;
                                    depthFrameData = new ushort[depthWidth * depthHeight];
                                    depthFrame.CopyFrameDataToArray(depthFrameData);
                                }
                            }
                        }
                    }
                    catch (Exception) { return; }
                }

                if (multiFrame.ColorFrameReference != null)
                {
                    try
                    {
                        using (ColorFrame colorFrame = multiFrame.ColorFrameReference.AcquireFrame())
                        {
                            if (colorFrame == null)
                                return;

                            colorFrameDescription = colorFrame.FrameDescription;
                            colorWidth = colorFrameDescription.Width;
                            colorHeight = colorFrameDescription.Height;
                            colorFrameData = new byte[colorWidth * colorHeight * bytesForPixelColor]; // 4 == bytes per color

                            using (KinectBuffer buffer = colorFrame.LockRawImageBuffer())
                            {
                                if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                                {
                                    colorFrame.CopyRawFrameDataToArray(colorFrameData);
                                }
                                else
                                {
                                    colorFrame.CopyConvertedFrameDataToArray(colorFrameData, ColorImageFormat.Bgra);
                                }
                            }
                            ColorSpacePoint[] _colorPoints = new ColorSpacePoint[depthWidth * depthHeight];
                            sensor.CoordinateMapper.MapDepthFrameToColorSpace(depthFrameData, _colorPoints);

                            rgbColorData = new System.Drawing.Color[depthWidth * depthHeight];

                            for (int y = 0, j = 0; y < depthHeight; y++)
                            {
                                for (int x = 0; x < depthWidth; x++, j++)
                                {
                                    int depthIndex = (y * depthWidth) + x;
                                    ColorSpacePoint colorPoint = _colorPoints[depthIndex];

                                    int colorX = (int)Math.Floor(colorPoint.X + 0.5);
                                    int colorY = (int)Math.Floor(colorPoint.Y + 0.5);

                                    if ((colorX >= 0) && (colorX < colorWidth) && (colorY >= 0) && (colorY < colorHeight))
                                    {
                                        int colorIndex = ((colorY * colorWidth) + colorX) * 4;
                                        rgbColorData[j] = System.Drawing.Color.FromArgb(colorFrameData[colorIndex - 2], colorFrameData[colorIndex + 1], colorFrameData[colorIndex]);
                                    }
                                    else
                                    {
                                        rgbColorData[j] = new System.Drawing.Color();
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception) { return; }
                }
            }
        }

        public static KinectSensor Sensor
        {
            get
            {
                if (sensor == null)
                {
                    Initialize();
                }
                return sensor;
            }
            set
            {
                sensor = value;
            }
        }
    }
}