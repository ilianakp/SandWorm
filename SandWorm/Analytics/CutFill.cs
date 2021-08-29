using System.Collections.Generic;
using System.Drawing;
using Rhino.Display;
using Rhino.Geometry;


namespace SandWorm.Analytics
{
    public class CutFill : Analysis.MeshColorAnalysis
    {
        private Structs.ColorPalettes _colorPalette = Structs.ColorPalettes.Europe;
        List<Color> paletteSwatches; // Color values for the given palette

        public CutFill() : base("Visualise Cut & Fill")
        {
        }

        private Color GetColorForCutFill(int cutFillValue, double gradientRange)
        {
            if (cutFillValue < 0)
                return lookupTable[0];
            else if (cutFillValue > 2 * gradientRange) // Since gradientRange defines a span in both negative and positive directions, we multiply it by 2 to get the whole range
                return lookupTable[lookupTable.Length - 1];
            else
                return lookupTable[cutFillValue];

        }
        public Color[] GetColorCloudForAnalysis(Point3d[] pointCoordinates, double?[] baseMeshElevationPoints, double gradientRange,
            Structs.ColorPalettes colorPalette, List<Color> customColors, ref List<string> stats, double unitsMultiplier)
        {
            double _cut = 0.0;
            double _fill = 0.0;
            double _area = 0.0;
            double _previousArea = 0.0;
            double _maxArea = 500; // Arbitrary value to check against

            _colorPalette = colorPalette;
            if (lookupTable == null)
            {
                paletteSwatches = ColorPalettes.GenerateColorPalettes(_colorPalette, customColors);
                ComputeLookupTableForAnalysis(0.0, gradientRange * 3, paletteSwatches.Count);
            }

            Color[] vertexColors = new Color[pointCoordinates.Length];

            for (int i = 0; i < baseMeshElevationPoints.Length; i++)
            {
                if (baseMeshElevationPoints[i] == null)
                    vertexColors[i] = Color.FromArgb(50, 50, 50); // All pixels which are outside of the provided mesh are marked as dark grey
                else
                {
                    double _difference = pointCoordinates[i].Z - (double)baseMeshElevationPoints[i];
                    vertexColors[i] = GetColorForCutFill((int)(_difference + gradientRange), gradientRange); // Add gradientRange to accommodate for negative values from cut operations
                    
                    if (i < pointCoordinates.Length - 1) // Assume dX == dY
                        _area = System.Math.Pow(pointCoordinates[i].X - pointCoordinates[i + 1].X, 2) / unitsMultiplier / unitsMultiplier;
                    
                    if (_area > _maxArea) // Make sure that area is correct when a pixel jumps from one side of the table to the other
                        _area = _previousArea;
                    else 
                        _previousArea = _area;
                    
                    if (_difference > 0)
                        _fill += _difference * _area / unitsMultiplier;
                    else
                        _cut += _difference * _area / unitsMultiplier;
                }
            }
            
            stats.Add($"Cut Volume, in cubic centimeters:");
            stats.Add(System.Math.Round(_cut * 0.001).ToString());
            stats.Add($"Fill Volume, in cubic centimeters:");
            stats.Add(System.Math.Round(_fill * 0.001).ToString());
            stats.Add($"Cut/Fill Balance, in cubic centimeters:");
            stats.Add(System.Math.Round((_cut + _fill) * 0.001).ToString());
            return vertexColors;
        }


        public override void ComputeLookupTableForAnalysis(double sensorElevation, double gradientRange, int swatchCount)
        {
            var elevationRanges = new Analysis.VisualisationRangeWithColor[swatchCount - 1];
            for (int i = 0; i < swatchCount - 1; i++)
            {
                var elevationRange = new Analysis.VisualisationRangeWithColor
                {
                    ValueSpan = (int)(gradientRange / swatchCount),
                    ColorStart = new ColorHSL(paletteSwatches[i]),
                    ColorEnd = new ColorHSL(paletteSwatches[i + 1])
                };
                elevationRanges[i] = elevationRange;
            }

            ComputeLinearRanges(elevationRanges);
        }

        public static double?[] MeshToPointArray(Mesh baseMesh, Point3d[] pointCoordinates)
        {
            double?[] pointElevationArray = new double?[pointCoordinates.Length];

            for (int i = 0; i < pointCoordinates.Length; i++)
            {
                // This needs to be done for each point individually. Providing an array of points would return only the points which were hit, disregarding all remaining ones
                var projectedPoints = Rhino.Geometry.Intersect.Intersection.ProjectPointsToMeshes(new[] { baseMesh }, new[] { pointCoordinates[i] }, new Vector3d(0, 0, 1), 0.001);
                if (projectedPoints != null && projectedPoints.Length != 0)
                    pointElevationArray[i] = projectedPoints[0].Z;
            }
                
            return pointElevationArray;
        }
    }
}
