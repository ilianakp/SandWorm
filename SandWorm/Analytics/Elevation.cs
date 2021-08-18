using System.Drawing;
using System.Collections.Generic;
using Rhino.Display;

namespace SandWorm.Analytics
{
    public class Elevation : Analysis.MeshColorAnalysis
    {
        private int _lastSensorElevation; // Keep track of prior values to recalculate only as needed
        private double _lastGradientRange;
        private Structs.ColorPalettes _colorPalette = Structs.ColorPalettes.Europe;
        Color[] paletteSwatches; // Color values for the given palette

        public Elevation() : base("Visualise Elevation")
        {
        }

        public Color[] GetColorCloudForAnalysis(double[] pixelArray, double sensorElevation, double gradientRange,
                                                Structs.ColorPalettes colorPalette, List<Color> customColors)
        {
            _colorPalette = colorPalette;
            var sensorElevationRounded = (int)sensorElevation; // Convert once as it is done often
            if (lookupTable == null || sensorElevationRounded != _lastSensorElevation || gradientRange != _lastGradientRange)
            {
                paletteSwatches = ColorPalettes.GenerateColorPalettes(_colorPalette, customColors);
                ComputeLookupTableForAnalysis(sensorElevation, gradientRange, paletteSwatches.Length);
            }

            // Lookup elevation value in color table
            var vertexColors = new Color[pixelArray.Length];
            for (int i = 0; i < pixelArray.Length; i++)
            {
                var pixelDepthNormalised = sensorElevationRounded - (int)pixelArray[i];
                if (pixelDepthNormalised < 0)
                    pixelDepthNormalised = 0; // Account for negative depths
                if (pixelDepthNormalised >= lookupTable.Length)
                    pixelDepthNormalised = lookupTable.Length - 1; // Account for big height

                vertexColors[i] = lookupTable[pixelDepthNormalised]; // Lookup z value in color table
            }
            return vertexColors;
        }

        // Given the sensor's height from the table, map between vertical distance intervals and color palette values
        public override void ComputeLookupTableForAnalysis(double sensorElevation, double gradientRange, int swatchCount)
        {
            var elevationRanges = new Analysis.VisualisationRangeWithColor[swatchCount];
            for (int i = 0; i < swatchCount; i++)
            {
                var elevationRange = new Analysis.VisualisationRangeWithColor
                {
                    ValueSpan = (int)(gradientRange / swatchCount),
                    ColorStart = new ColorHSL(paletteSwatches[i]),
                    ColorEnd = new ColorHSL(paletteSwatches[i+1])
                };
                elevationRanges[i] = elevationRange;
            }

            ComputeLinearRanges(elevationRanges);
            _lastSensorElevation = (int)sensorElevation;
            _lastGradientRange = gradientRange;
        }
    }
}