
namespace SandWorm
{
    public static class Structs
    {
        public enum KinectTypes
        {
            KinectAzureNear,
            KinectAzureWide,
            KinectForWindows
        }

        public enum OutputTypes
        {
            Mesh,
            PointCloud
        }

        public enum AnalysisTypes
        {
            None,
            RGB,
            Elevation,
            Slope,
            Aspect,
            CutFill
        }
    }
}
