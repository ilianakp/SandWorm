using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Numerics;
using Rhino.Display;
using Rhino.Geometry;
using System.Linq;

namespace SandWorm.Analytics
{
    public class Agent : Analysis.MeshColorAnalysis
    {
        readonly ushort maximumSlope = 1000; // Needs to be some form of cutoff to keep lookup table small; this = ~84%
        readonly ushort maximumAgentDelta = 1000; // Needs to be some form of cutoff to keep lookup table small; this = ~84%
        readonly int agentsMax = 1000; //
        readonly int agentLife = 1000; //
        readonly double minPreferredSlope = 0;
        readonly double maxPreferredSlope = 0.3;

        int[] agentPositions = null;

        private static readonly Random getrandom = new Random();


        public Agent() : base("Visualise Agents")
        {
        }

        private Color GetColorForSlope(ushort slopeValue)
        {
            if (slopeValue > maximumSlope)
                return lookupTable[lookupTable.Length - 1];
            else
                return lookupTable[slopeValue];
        }

        public Color[] GetColorCloudForAnalysis(double[] pixelArray, int width, int height, double gradientRange, Vector2[] xyLookupTable)
        {
            if (lookupTable == null)
                ComputeLookupTableForAnalysis(0.0, gradientRange);

            double deltaX;
            double deltaY;
            double deltaXY;
            double slope = 0.0;
            double[] slopeArray = new double[width * height];

            var vertexColors = new Color[pixelArray.Length];

            // first pixel NW
            deltaX = (xyLookupTable[1].X - xyLookupTable[0].X);
            deltaY = (xyLookupTable[width].Y - xyLookupTable[0].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slope += Math.Abs(pixelArray[1] - pixelArray[0]) / deltaX; // E Pixel
            slope += Math.Abs(pixelArray[width] - pixelArray[0]) / deltaY; // S Pixel
            slope += Math.Abs(pixelArray[width + 1] - pixelArray[0]) / deltaXY; // SE Pixel

            slopeArray[0] = slope;
            //vertexColors[0] = GetColorForSlope((ushort)(slope * 33.33)); // Divide by 3 multiply by 100 => 33.33

            // last pixel NE
            deltaX = (xyLookupTable[width - 2].X - xyLookupTable[width - 1].X);
            deltaY = (xyLookupTable[2 * width - 1].Y - xyLookupTable[width - 1].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slope = 0.0;
            slope += Math.Abs(pixelArray[width - 2] - pixelArray[width - 1]) / deltaX; // W Pixel
            slope += Math.Abs(pixelArray[2 * width - 1] - pixelArray[width - 1]) / deltaY; // S Pixel
            slope += Math.Abs(pixelArray[2 * width - 2] - pixelArray[width - 1]) / deltaXY; // SW Pixel

            slopeArray[width - 1] = slope;
            //vertexColors[width - 1] = GetColorForSlope((ushort)(slope * 33.33)); // Divide by 3 multiply by 100 => 33.33

            // first pixel SW
            deltaX = (xyLookupTable[(height - 1) * width + 1].X - xyLookupTable[(height - 1) * width].X);
            deltaY = (xyLookupTable[(height - 2) * width].Y - xyLookupTable[(height - 1) * width].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slope = 0.0;
            slope += Math.Abs(pixelArray[(height - 1) * width + 1] - pixelArray[(height - 1) * width]) / deltaX; // E Pixel
            slope += Math.Abs(pixelArray[(height - 2) * width] - pixelArray[(height - 1) * width]) / deltaY; // N Pixel
            slope += Math.Abs(pixelArray[(height - 2) * width + 1] - pixelArray[(height - 1) * width]) / deltaXY; //NE Pixel

            slopeArray[(height - 1) * width] = slope;
            //vertexColors[(height - 1) * width] = GetColorForSlope((ushort)(slope * 33.33)); // Divide by 3 multiply by 100 => 33.33

            // last pixel SE
            deltaX = (xyLookupTable[height * width - 2].X - xyLookupTable[height * width - 1].X);
            deltaY = (xyLookupTable[(height - 1) * width - 1].Y - xyLookupTable[height * width - 1].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slope = 0.0;
            slope += Math.Abs(pixelArray[height * width - 2] - pixelArray[height * width - 1]) / deltaX; // W Pixel
            slope += Math.Abs(pixelArray[(height - 1) * width - 1] - pixelArray[height * width - 1]) / deltaY; // N Pixel
            slope += Math.Abs(pixelArray[(height - 1) * width - 2] - pixelArray[height * width - 1]) / deltaXY; //NW Pixel

            slopeArray[height * width - 1] = slope;
            //vertexColors[height * width - 1] = GetColorForSlope((ushort)(slope * 33.33)); // Divide by 3 multiply by 100 => 33.33

            // first row
            for (int i = 1; i < width - 1; i++)
            {
                deltaX = (xyLookupTable[i - 1].X - xyLookupTable[i].X);
                deltaY = (xyLookupTable[i + width].Y - xyLookupTable[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slope = 0.0;
                slope += Math.Abs(pixelArray[i - 1] - pixelArray[i]) / deltaX; // W Pixel
                slope += Math.Abs(pixelArray[i + 1] - pixelArray[i]) / deltaX; // E Pixel
                slope += Math.Abs(pixelArray[i + width - 1] - pixelArray[i]) / deltaXY; // SW Pixel
                slope += Math.Abs(pixelArray[i + width] - pixelArray[i]) / deltaY; // S Pixel
                slope += Math.Abs(pixelArray[i + width + 1] - pixelArray[i]) / deltaXY; // SE Pixel

                slopeArray[i] = slope;
                //vertexColors[i] = GetColorForSlope((ushort)(slope * 20.0)); // Divide by 5 multiply by 100 => 20.0
            }

            // last row
            for (int i = (height - 1) * width + 1; i < height * width - 1; i++)
            {
                deltaX = (xyLookupTable[i - 1].X - xyLookupTable[i].X);
                deltaY = (xyLookupTable[i - width].Y - xyLookupTable[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slope = 0.0;
                slope += Math.Abs(pixelArray[i - 1] - pixelArray[i]) / deltaX; // W Pixel
                slope += Math.Abs(pixelArray[i + 1] - pixelArray[i]) / deltaX; // E Pixel
                slope += Math.Abs(pixelArray[i - width - 1] - pixelArray[i]) / deltaXY; // NW Pixel
                slope += Math.Abs(pixelArray[i - width] - pixelArray[i]) / deltaY; // N Pixel
                slope += Math.Abs(pixelArray[i - width + 1] - pixelArray[i]) / deltaXY; // NE Pixel

                slopeArray[i] = slope;
                //vertexColors[i] = GetColorForSlope((ushort)(slope * 20.0)); // Divide by 5 multiply by 100 => 20.0
            }

            // first column
            for (int i = width; i < (height - 1) * width; i += width)
            {
                deltaX = (xyLookupTable[i - width].X - xyLookupTable[i].X);
                deltaY = (xyLookupTable[i + 1].Y - xyLookupTable[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slope = 0.0;
                slope += Math.Abs(pixelArray[i - width] - pixelArray[i]) / deltaY; // N Pixel
                slope += Math.Abs(pixelArray[i + width] - pixelArray[i]) / deltaY; // S Pixel
                slope += Math.Abs(pixelArray[i - width + 1] - pixelArray[i]) / deltaXY; // NE Pixel
                slope += Math.Abs(pixelArray[i + 1] - pixelArray[i]) / deltaX; // E Pixel
                slope += Math.Abs(pixelArray[i + width + 1] - pixelArray[i]) / deltaXY; // SE Pixel

                slopeArray[i] = slope;
                //vertexColors[i] = GetColorForSlope((ushort)(slope * 20.0)); // Divide by 5 multiply by 100 => 20.0
            }

            // last column
            for (int i = 2 * width - 1; i < height * width - 1; i += width)
            {
                deltaX = (xyLookupTable[i - width].X - xyLookupTable[i].X);
                deltaY = (xyLookupTable[i - 1].Y - xyLookupTable[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slope = 0.0;
                slope += Math.Abs(pixelArray[i - width] - pixelArray[i]) / deltaY; // N Pixel
                slope += Math.Abs(pixelArray[i + width] - pixelArray[i]) / deltaY; // S Pixel
                slope += Math.Abs(pixelArray[i - width - 1] - pixelArray[i]) / deltaXY; // NW Pixel
                slope += Math.Abs(pixelArray[i - 1] - pixelArray[i]) / deltaX; // W Pixel
                slope += Math.Abs(pixelArray[i + width - 1] - pixelArray[i]) / deltaXY; // SW Pixel

                slopeArray[i] = slope;
                //vertexColors[i] = GetColorForSlope((ushort)(slope * 20.0)); // Divide by 5 multiply by 100 => 20.0
            }

            // rest of the array
            Parallel.For(1, height - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 1; columns < width - 1; columns++)             // Iterate over x dimension
                {
                    int h = (rows - 1) * width + columns;
                    int i = rows * width + columns;
                    int j = (rows + 1) * width + columns;

                    double parallelSlope = 0.0; // Declare a local variable in the parallel loop for performance reasons

                    double parallelDeltaX = (xyLookupTable[i + 1].X - xyLookupTable[i].X);
                    double parallelDeltaY = (xyLookupTable[h].Y - xyLookupTable[i].Y);
                    double parallelDeltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                    parallelSlope += Math.Abs((pixelArray[h - 1] - pixelArray[i])) / parallelDeltaXY; // NW pixel
                    parallelSlope += Math.Abs((pixelArray[h] - pixelArray[i])) / parallelDeltaY; //N pixel
                    parallelSlope += Math.Abs((pixelArray[h + 1] - pixelArray[i])) / parallelDeltaXY; //NE pixel
                    parallelSlope += Math.Abs((pixelArray[i - 1] - pixelArray[i])) / parallelDeltaX; //W pixel
                    parallelSlope += Math.Abs((pixelArray[i + 1] - pixelArray[i])) / parallelDeltaX; //E pixel
                    parallelSlope += Math.Abs((pixelArray[j - 1] - pixelArray[i])) / parallelDeltaXY; //SW pixel
                    parallelSlope += Math.Abs((pixelArray[j] - pixelArray[i])) / parallelDeltaY; //S pixel
                    parallelSlope += Math.Abs((pixelArray[j + 1] - pixelArray[i])) / parallelDeltaXY; //SE pixel

                    slopeArray[i] = parallelSlope;
                    //vertexColors[i] = GetColorForSlope((ushort)(parallelSlope * 12.5)); // Divide by 8 multiply by 100 => 12.5
                }
            });

            // Keep track of agents
            if (agentPositions == null)
                agentPositions = new int[width * height];
            //var agentDestinations = new List<int>();
            var agentDestinations = new List<Point2d>();
            var agentPositionsAsPoints = new List<Point2d>();


            // Spawn agents somwhere on border

            if (Enumerable.Sum(agentPositions) < agentsMax)
            {
                int randomnumberx = getrandom.Next(0, width-1);
                int randomnumbery = getrandom.Next(0, height-1);
                int i = randomnumberx * height + randomnumbery;
                agentPositions[i] ++; //Marks Position and Quantity
                agentPositionsAsPoints.Add(new Point2d(randomnumberx, randomnumbery));

                // Create random destinations for each agent created
                int destinationx = getrandom.Next(0, width-1);
                int destinationy = getrandom.Next(0, height-1);
                agentDestinations.Add(new Point2d(destinationx, destinationy));
            }


            // Walk agents towards acceptable slope to reach destination as class instance
            for (int i = 0; i < agentPositionsAsPoints.Count(); i++)
            {

            }

            // Walk agents towards acceptable slope to reach destination as pixel array
            for(int rows = 0; rows < height-1; rows++)
            {
                for(int columns = 0; columns < width-1; columns++)
                
                {
                    int h = (rows - 1) * width + columns;
                    int i = rows * width + columns;
                    int j = (rows + 1) * width + columns;
                    int[] D8Index = new int[8] { h, h + 1, i + 1, j + 1, j, j - 1, i - 1, h - 1 };
                    Point2d[] D8DeltaDestination = new Point2d[8];

                    for (int k = 0; k < agentPositions[i]; k++)
                    {
                        agentPositions[i]--;
                        agentPositions[D8Index[getrandom.Next(0,8)]]++; //Random movement
                        // Add unitised likelyhood based on destination
                        for (int u = 0; u < 8; u++)
                        {
                            D8DeltaDestination[u].X = rows;
                            D8DeltaDestination[u].Y = columns;

                            
        }


                        // Add likelyhood based on slope
                    }



                }
            }
                    



            // Delete agents that reach their destination


            // Show agent state
            Parallel.For(0, height - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 0; columns < width - 1; columns++)             // Iterate over x dimension
                {
                    int h = (rows - 1) * width + columns;
                    int i = rows * width + columns;
                    int j = (rows + 1) * width + columns;

                    vertexColors[i] = GetColorForSlope((ushort)(agentPositions[i] * 50));

                }
            });


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

