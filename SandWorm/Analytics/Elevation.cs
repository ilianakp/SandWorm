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
        Color[] colorPalettes;

        public Elevation() : base("Visualise Elevation")
        {
        }

        public Color[] GetColorCloudForAnalysis(double[] pixelArray, double sensorElevation, double gradientRange, Structs.ColorPalettes colorPalette, List<Color> customColors)
        {
            _colorPalette = colorPalette;
            var sensorElevationRounded = (int)sensorElevation; // Convert once as it is done often
            if (lookupTable == null || sensorElevationRounded != _lastSensorElevation || gradientRange != _lastGradientRange)
            {
                colorPalettes = ColorPalettes.GenerateColorPalettes(_colorPalette, customColors);
                
                ComputeLookupTableForAnalysis(sensorElevation, gradientRange);
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

        public override void ComputeLookupTableForAnalysis(double sensorElevation, double gradientRange)
        {
            var sElevationRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)(gradientRange / 4),
                ColorStart = new ColorHSL(colorPalettes[0]),
                ColorEnd = new ColorHSL(colorPalettes[1])
            };
            var mElevationRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)(gradientRange / 4),
                ColorStart = new ColorHSL(colorPalettes[1]),
                ColorEnd = new ColorHSL(colorPalettes[2])
            };
            var lElevationRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)(gradientRange / 4),
                ColorStart = new ColorHSL(colorPalettes[2]),
                ColorEnd = new ColorHSL(colorPalettes[3])
            };
            var xlElevationRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)(gradientRange / 4),
                ColorStart = new ColorHSL(colorPalettes[3]),
                ColorEnd = new ColorHSL(colorPalettes[4])
            };

            ComputeLinearRanges(sElevationRange, mElevationRange, lElevationRange, xlElevationRange);
            _lastSensorElevation = (int)sensorElevation;
            _lastGradientRange = gradientRange;
        }
    }
}