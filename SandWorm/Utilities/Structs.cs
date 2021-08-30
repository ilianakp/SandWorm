
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
            RainDensity,
            Agent,
            Aspect,
            CutFill
        }

        public enum ColorPalettes
        {
            Custom,
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
