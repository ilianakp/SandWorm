using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Threading.Tasks;
using Rhino.Display;
using System.Linq;
using OpenCvSharp;
using System.Collections.Concurrent;

namespace SandWorm
{
    public class Slope : Analysis.MeshColorAnalysis
    {
        readonly ushort maximumSlope = 1000; // Needs to be some form of cutoff to keep lookup table small; this = ~84%

        public Slope() : base("Visualise Slope")
        {
        }

        private Color GetColorForSlope(ushort slopeValue)
        {
            if (slopeValue > maximumSlope)
                return lookupTable[lookupTable.Length - 1];
            else
                return lookupTable[slopeValue];
        }

        public Color[] GetColorCloudForAnalysis(double[] pixelArray, int width, int height, double gradientRange, Vector2[] xyLookupTable, Rhino.Geometry.Point3d[] pts, out List<double> slopesOut, out List<Rhino.Geometry.Point3d> vertsOut)
        {
            if (lookupTable == null)
                ComputeLookupTableForAnalysis(0.0, gradientRange);

            double deltaX;
            double deltaY;
            double deltaXY;
            double slope = 0.0;

            var vertexColors = new Color[pixelArray.Length];
            
            //double[] slopes = new double[pixelArray.Length];
            //Rhino.Geometry.Point3d[] verts = new Rhino.Geometry.Point3d[pixelArray.Length];
            ConcurrentBag<double> slopes = new ConcurrentBag<double>();
            ConcurrentBag<Rhino.Geometry.Point3d> verts = new ConcurrentBag<Rhino.Geometry.Point3d>();

            #region first pixel NW
            deltaX = (xyLookupTable[1].X - xyLookupTable[0].X);
            deltaY = (xyLookupTable[width].Y - xyLookupTable[0].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slope += Math.Abs(pixelArray[1] - pixelArray[0]) * SandWormComponent.unitsMultiplier / deltaX; // E Pixel
            slope += Math.Abs(pixelArray[width] - pixelArray[0]) * SandWormComponent.unitsMultiplier / deltaY; // S Pixel
            slope += Math.Abs(pixelArray[width + 1] - pixelArray[0]) * SandWormComponent.unitsMultiplier / deltaXY; // SE Pixel

            vertexColors[0] = GetColorForSlope((ushort)(slope * 33.33)); // Divide by 3 multiply by 100 => 33.33
            #endregion

            #region last pixel NE
            deltaX = (xyLookupTable[width - 2].X - xyLookupTable[width - 1].X);
            deltaY = (xyLookupTable[2 * width - 1].Y - xyLookupTable[width - 1].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slope = 0.0;
            slope += Math.Abs(pixelArray[width - 2] - pixelArray[width - 1]) * SandWormComponent.unitsMultiplier / deltaX; // W Pixel
            slope += Math.Abs(pixelArray[2 * width - 1] - pixelArray[width - 1]) * SandWormComponent.unitsMultiplier / deltaY; // S Pixel
            slope += Math.Abs(pixelArray[2 * width - 2] - pixelArray[width - 1]) * SandWormComponent.unitsMultiplier / deltaXY; // SW Pixel

            vertexColors[width - 1] = GetColorForSlope((ushort)(slope * 33.33)); // Divide by 3 multiply by 100 => 33.33
            #endregion

            #region first pixel SW
            deltaX = (xyLookupTable[(height - 1) * width + 1].X - xyLookupTable[(height - 1) * width].X);
            deltaY = (xyLookupTable[(height - 2) * width].Y - xyLookupTable[(height - 1) * width].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slope = 0.0;
            slope += Math.Abs(pixelArray[(height - 1) * width + 1] - pixelArray[(height - 1) * width]) * SandWormComponent.unitsMultiplier / deltaX; // E Pixel
            slope += Math.Abs(pixelArray[(height - 2) * width] - pixelArray[(height - 1) * width]) * SandWormComponent.unitsMultiplier / deltaY; // N Pixel
            slope += Math.Abs(pixelArray[(height - 2) * width + 1] - pixelArray[(height - 1) * width]) * SandWormComponent.unitsMultiplier / deltaXY; //NE Pixel

            //slopes[(height - 1) * width] = slope * 33.33;
            vertexColors[(height - 1) * width] = GetColorForSlope((ushort)(slope * 33.33)); // Divide by 3 multiply by 100 => 33.33
            #endregion

            #region last pixel SE
            deltaX = (xyLookupTable[height * width - 2].X - xyLookupTable[height * width - 1].X);
            deltaY = (xyLookupTable[(height - 1) * width - 1].Y - xyLookupTable[height * width - 1].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slope = 0.0;
            slope += Math.Abs(pixelArray[height * width - 2] - pixelArray[height * width - 1]) * SandWormComponent.unitsMultiplier / deltaX; // W Pixel
            slope += Math.Abs(pixelArray[(height - 1) * width - 1] - pixelArray[height * width - 1]) * SandWormComponent.unitsMultiplier / deltaY; // N Pixel
            slope += Math.Abs(pixelArray[(height - 1) * width - 2] - pixelArray[height * width - 1]) * SandWormComponent.unitsMultiplier / deltaXY; //NW Pixel

            vertexColors[height * width - 1] = GetColorForSlope((ushort)(slope * 33.33)); // Divide by 3 multiply by 100 => 33.33
            #endregion

            #region first row
            for (int i = 1; i < width - 1; i++)
            {
                deltaX = (xyLookupTable[i - 1].X - xyLookupTable[i].X);
                deltaY = (xyLookupTable[i + width].Y - xyLookupTable[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slope = 0.0;
                slope += Math.Abs(pixelArray[i - 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaX; // W Pixel
                slope += Math.Abs(pixelArray[i + 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaX; // E Pixel
                slope += Math.Abs(pixelArray[i + width - 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaXY; // SW Pixel
                slope += Math.Abs(pixelArray[i + width] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaY; // S Pixel
                slope += Math.Abs(pixelArray[i + width + 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaXY; // SE Pixel

                vertexColors[i] = GetColorForSlope((ushort)(slope * 20.0)); // Divide by 5 multiply by 100 => 20.0
            }
            #endregion
            #region last row
            for (int i = (height - 1) * width + 1; i < height * width - 1; i++)
            {
                deltaX = (xyLookupTable[i - 1].X - xyLookupTable[i].X);
                deltaY = (xyLookupTable[i - width].Y - xyLookupTable[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slope = 0.0;
                slope += Math.Abs(pixelArray[i - 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaX; // W Pixel
                slope += Math.Abs(pixelArray[i + 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaX; // E Pixel
                slope += Math.Abs(pixelArray[i - width - 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaXY; // NW Pixel
                slope += Math.Abs(pixelArray[i - width] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaY; // N Pixel
                slope += Math.Abs(pixelArray[i - width + 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaXY; // NE Pixel

                vertexColors[i] = GetColorForSlope((ushort)(slope * 20.0)); // Divide by 5 multiply by 100 => 20.0
            }
            #endregion

            #region first column
            for (int i = width; i < (height - 1) * width; i += width)
            {
                deltaX = (xyLookupTable[i - width].X - xyLookupTable[i].X);
                deltaY = (xyLookupTable[i + 1].Y - xyLookupTable[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slope = 0.0;
                slope += Math.Abs(pixelArray[i - width] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaY; // N Pixel
                slope += Math.Abs(pixelArray[i + width] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaY; // S Pixel
                slope += Math.Abs(pixelArray[i - width + 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaXY; // NE Pixel
                slope += Math.Abs(pixelArray[i + 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaX; // E Pixel
                slope += Math.Abs(pixelArray[i + width + 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaXY; // SE Pixel

                vertexColors[i] = GetColorForSlope((ushort)(slope * 20.0)); // Divide by 5 multiply by 100 => 20.0
            }
            #endregion

            #region last column
            for (int i = 2 * width - 1; i < height * width - 1; i += width)
            {
                deltaX = (xyLookupTable[i - width].X - xyLookupTable[i].X);
                deltaY = (xyLookupTable[i - 1].Y - xyLookupTable[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slope = 0.0;
                slope += Math.Abs(pixelArray[i - width] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaY; // N Pixel
                slope += Math.Abs(pixelArray[i + width] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaY; // S Pixel
                slope += Math.Abs(pixelArray[i - width - 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaXY; // NW Pixel
                slope += Math.Abs(pixelArray[i - 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaX; // W Pixel
                slope += Math.Abs(pixelArray[i + width - 1] - pixelArray[i]) * SandWormComponent.unitsMultiplier / deltaXY; // SW Pixel

                vertexColors[i] = GetColorForSlope((ushort)(slope * 20.0)); // Divide by 5 multiply by 100 => 20.0
            }
            #endregion

            int skipFactor = 10;
            // rest of the array
            // Switch it to for debug
            //skip rows and columns with the same number
            //
            Parallel.For(1, height - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 1; columns < width - 1; columns++)          // Iterate over x dimension
                {
                    int i = rows * width + columns;
                    int h = i - width;
                    int j = i + width;

                    double parallelSlope = 0.0; // Declare a local variable in the parallel loop for performance reasons

                    double parallelDeltaX = (xyLookupTable[i + 1].X - xyLookupTable[i].X);
                    double parallelDeltaY = (xyLookupTable[h].Y - xyLookupTable[i].Y);
                    double parallelDeltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                    parallelSlope += Math.Abs((pixelArray[h - 1] - pixelArray[i])) * SandWormComponent.unitsMultiplier / parallelDeltaXY; //NW pixel
                    parallelSlope += Math.Abs((pixelArray[h] - pixelArray[i])) * SandWormComponent.unitsMultiplier / parallelDeltaY; //N pixel
                    parallelSlope += Math.Abs((pixelArray[h + 1] - pixelArray[i])) * SandWormComponent.unitsMultiplier / parallelDeltaXY; //NE pixel
                    parallelSlope += Math.Abs((pixelArray[i - 1] - pixelArray[i])) * SandWormComponent.unitsMultiplier / parallelDeltaX; //W pixel
                    parallelSlope += Math.Abs((pixelArray[i + 1] - pixelArray[i])) * SandWormComponent.unitsMultiplier / parallelDeltaX; //E pixel
                    parallelSlope += Math.Abs((pixelArray[j - 1] - pixelArray[i])) * SandWormComponent.unitsMultiplier / parallelDeltaXY; //SW pixel
                    parallelSlope += Math.Abs((pixelArray[j] - pixelArray[i])) * SandWormComponent.unitsMultiplier / parallelDeltaY; //S pixel
                    parallelSlope += Math.Abs((pixelArray[j + 1] - pixelArray[i])) * SandWormComponent.unitsMultiplier / parallelDeltaXY; //SE pixel

                    double finalSlope = parallelSlope * 12.5;
                    if (rows % skipFactor == 0 && columns % skipFactor == 0)
                    { 
                        slopes.Add(finalSlope);
                        verts.Add(pts[i]);
                    }
                    vertexColors[i] = GetColorForSlope((ushort)(finalSlope)); // Divide by 8 multiply by 100 => 12.5
                }
            });

            slopesOut = slopes.ToList();
            vertsOut = verts.ToList();

            return vertexColors;
        }

        public override void ComputeLookupTableForAnalysis(double sensorElevation, double gradientRange)
        {
            var slightSlopeRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)gradientRange,
                ColorStart = new ColorHSL(0.30, 1.0, 0.5), // Green
                ColorEnd = new ColorHSL(0.15, 1.0, 0.5) // Yellow
            };
            var moderateSlopeRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)gradientRange,
                ColorStart = new ColorHSL(0.15, 1.0, 0.5), // Yellow
                ColorEnd = new ColorHSL(0.0, 1.0, 0.5) // Red
            };
            var extremeSlopeRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = maximumSlope - slightSlopeRange.ValueSpan - moderateSlopeRange.ValueSpan,
                ColorStart = new ColorHSL(0.0, 1.0, 0.5), // Red
                ColorEnd = new ColorHSL(0.0, 1.0, 0.0) // Black
            };
            ComputeLinearRanges(slightSlopeRange, moderateSlopeRange, extremeSlopeRange);
        }
    }
}