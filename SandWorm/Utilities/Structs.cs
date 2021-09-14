
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
            Camera,
            Elevation,
            Slope,
            Aspect,
            CutFill
        }

        public enum ColorPalettes
        {
            Custom,
            CutFill,
            Chile,
            Desert,
            Dune,
            Europe,
            Greyscale,
            Ocean,
            Turbo,
            Viridis
        }
    }
}
