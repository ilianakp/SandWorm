using Rhino.Display;
using Rhino.Geometry;
using System.Drawing;

namespace SandWorm.Analytics
{
    public class CutFill : Analysis.MeshColorAnalysis
    {
        public CutFill() : base("Visualise Cut & Fill")
        {
        }

        private Color GetColorForCutFill(int cutFillValue, double gradientRange)
        {
            if (cutFillValue < 0)
                return lookupTable[0];
            else if (cutFillValue > 2 * gradientRange)
                return lookupTable[lookupTable.Length - 1];
            else
                return lookupTable[cutFillValue];

        }
        public Color[] GetColorCloudForAnalysis(Point3d[] pointCoordinates, double?[] baseMeshElevationPoints, double gradientRange)
        {
            if (lookupTable == null)
                ComputeLookupTableForAnalysis(0.0, gradientRange);

            Color[] vertexColors = new Color[pointCoordinates.Length];

            for (int i = 0; i < baseMeshElevationPoints.Length; i++)
            {
                if (baseMeshElevationPoints[i] == null)
                    vertexColors[i] = Color.FromArgb(50, 50, 50);
                else
                    vertexColors[i] = GetColorForCutFill((int)(pointCoordinates[i].Z - baseMeshElevationPoints[i] + gradientRange), gradientRange);
            }
            return vertexColors;
        }


        public override void ComputeLookupTableForAnalysis(double sensorElevation, double gradientRange)
        {
            var cut = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)gradientRange,
                ColorStart = new ColorHSL(1.0, 1.0, 0.3), // Dark Red
                ColorEnd = new ColorHSL(1.0, 1.0, 1.0) // White
            };
            var fill = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)gradientRange,
                ColorStart = new ColorHSL(0.3, 1.0, 1.0), // White
                ColorEnd = new ColorHSL(0.3, 1.0, 0.3) // Dark Green
            };
            ComputeLinearRanges(cut, fill);
        }

        public static double?[] MeshToPointArray(Mesh baseMesh, Point3d[] pointCoordinates)
        {
            double?[] pointElevationArray = new double?[pointCoordinates.Length];
            
            //Point3d[] projectedPoints = Rhino.Geometry.Intersect.Intersection.ProjectPointsToMeshes(new[] { baseMesh }, pointCoordinates, new Vector3d(0, 0, 1), 0);

            for (int i = 0; i < pointCoordinates.Length; i++)
            {
                var projectedPoints = Rhino.Geometry.Intersect.Intersection.ProjectPointsToMeshes(new[] { baseMesh }, new[] { pointCoordinates[i] }, new Vector3d(0, 0, 1), 0.001);
                if (projectedPoints.Length != 0)
                    pointElevationArray[i] = projectedPoints[0].Z;
            }
                

            return pointElevationArray;
        }
    }
}
