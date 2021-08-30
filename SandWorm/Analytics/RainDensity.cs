using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Rhino.Display;
using Rhino.Geometry;
using System.Linq;

namespace SandWorm.Analytics
{
    public class RainDensity : Analysis.MeshColorAnalysis
    {

        readonly ushort maximumRain = 1000; // Needs to be some form of cutoff to keep lookup table small; this = ~84%
        readonly double permeabilityFactor = 0.999; //Regulates the infiltration for each step.
        readonly double flowFactor = 0.5; //Flow % for each step, if rain is continously pouring set to 1 (otherwise it will stack)
        //double[] double combFactor = flowFactor * permeabilityFactor;
        double[] waterHeadLevel = null;


        public RainDensity() : base("Visualise Rain")
        {
        }

        private Color GetColorForRainAnalysis(ushort rainValue)
        {
            if (rainValue > maximumRain)
                return lookupTable[lookupTable.Length - 1];
            else
                return lookupTable[rainValue];
        }

        public Color[] GetColorCloudForAnalysis(double[] pixelArray, int width, int height, double deltaX, double deltaY, double gradientRange, int rainDensity)
        {

            // TODO this is a temporary hack. Needs proper logic to calculate actual XY distance between pixels for Kinect Azure
            if (lookupTable == null)
            {
                ComputeLookupTableForAnalysis(0.0, gradientRange);
            }

            if (waterHeadLevel == null) //initialise if it does not exist
            {
                waterHeadLevel = new double[width * height];
            }

            for (int i = 0; i < waterHeadLevel.Length; i += rainDensity)
            {
                waterHeadLevel[i] += 0.5;
            }

            double[] waterHeadLevelBuffer = new double[width * height];

            if (deltaX == 0)
            {
                deltaX = 1;
                deltaY = 1;
            }
            double deltaXY = 1.414;

            // Color Map
            var vertexColors = new Color[pixelArray.Length];

            // Buffer copy to store changes
            waterHeadLevelBuffer = new double[width * height];

            // Border disposes of any water it recieves 
            for (int rows = 0; rows < 1 | rows > height - 1; rows++)
            {
                for (int columns = 0; columns < 1 | columns > height - 1; columns++)
                {
                    int i = rows * width + columns;
                    waterHeadLevelBuffer[i] = 0;
                }
            }

            //  array without outer border
            for (int rows = 1; rows < height-1; rows++)         // Iterate over y dimension
                {
                for (int columns = 1; columns < width - 1; columns++)             // Iterate over x dimension
                    {
                        int h = (rows - 1) * width + columns;
                        int i = rows * width + columns;
                        int j = (rows + 1) * width + columns;

                        int[] D8Index = new int[8] { h, h + 1, i + 1, j + 1, j, j - 1, i - 1, h - 1 };
                        double[] D8Flow = new double[8];
                        double deltaCumulative = 0;
                        var currentHead = -pixelArray[i] + waterHeadLevel[i]; // Pixel array is inverted hence the -ve


                        // distance factors should account for diagonal being longer D4 = 1 D8 corners = 1.414. Pixel array is inverted hence the -ve
                        D8Flow[0] = (-pixelArray[h] + waterHeadLevel[h] - currentHead) / deltaY; //N pixel
                        D8Flow[1] = (-pixelArray[h + 1] + waterHeadLevel[h + 1] - currentHead) / deltaXY; //NE pixel
                        D8Flow[2] = (-pixelArray[i + 1]  + waterHeadLevel[i + 1] - currentHead) / deltaX; //E pixel
                        D8Flow[3] = (-pixelArray[j + 1]  + waterHeadLevel[j + 1] - currentHead) / deltaXY; //SE pixel
                        D8Flow[4] = (-pixelArray[j]  + waterHeadLevel[j] - currentHead) / deltaY; //S pixel
                        D8Flow[5] = (-pixelArray[j - 1]  + waterHeadLevel[j - 1] - currentHead) / deltaXY; //SW pixel
                        D8Flow[6] = (-pixelArray[i - 1]  + waterHeadLevel[i - 1] - currentHead) / deltaX; //W pixel
                        D8Flow[7] = (-pixelArray[h - 1]  + waterHeadLevel[h - 1] - currentHead) / deltaXY; // NW pixel
                
                        if (waterHeadLevel[i] > 0) // if current cell has any water to give
                            {
                                //available water is  (Other option is to take the difference from the highest to the second highest
                                for (int k = 0; k < 8; k++) // for D8 positions
                                {
                                    if (D8Flow[k] < 0) //Only consider if adjacent cell - current cell is negative. Ignores case where there is a same level head of water.
                                    {
                                        deltaCumulative += Math.Abs(D8Flow[k]);
                                    }
                                }

                                for (int k = 0; k < 8; k++)
                                {
                                    if (D8Flow[k] < 0)
                                    {
                                        // this is (cell flow/total flow) * permeabilityFactor * flowFactor * available water
                                        waterHeadLevelBuffer[D8Index[k]] += (Math.Abs(D8Flow[k]) / deltaCumulative) * permeabilityFactor * flowFactor * waterHeadLevel[i]; //+ waterHeadLevelBuffer[D8Index[k]]
                                    }
                                }
                                waterHeadLevelBuffer[i] = waterHeadLevel[i] + waterHeadLevelBuffer[i] - waterHeadLevel[i] * flowFactor;  //TODO CHANGE TO (1 - flowFactor) * waterHeadLevel[i]
                    }

                        else
                            {
                                waterHeadLevel[i] = 0; // cannot give negative water
                            }
                        
                    }
            
                };

            for (int rows = 1; rows < height - 1; rows++)         // Iterate over y dimension
            {
                for (int columns = 1; columns < width - 1; columns++)             // Iterate over x dimension
                {
                    int i = rows * width + columns;
                    vertexColors[i] = GetColorForRainAnalysis((ushort)(waterHeadLevelBuffer[i] * 100));
                }
            };

            if (waterHeadLevel.Any(e => e > maximumRain))
                waterHeadLevel = new double[width * height]; //Reset buffer before next pass
            else
                waterHeadLevel = waterHeadLevelBuffer; //Update buffer before next pass

            return vertexColors;



            
        }

        public override void ComputeLookupTableForAnalysis(double sensorElevation, double gradientRange)
        {
            var slightSlopeRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)gradientRange,
                ColorStart = new ColorHSL(0.0, 0.0, 1.0), // White
                ColorEnd = new ColorHSL(0.66, 1.0, 0.66) // Light Blue
            };
            var moderateSlopeRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = (int)gradientRange,
                ColorStart = new ColorHSL(0.66, 1.0, 0.66), // Light Blue
                ColorEnd = new ColorHSL(0.66, 1.0, 0.33) // Mid Blue
            };
            var extremeSlopeRange = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = maximumRain - slightSlopeRange.ValueSpan - moderateSlopeRange.ValueSpan,
                ColorStart = new ColorHSL(0.66, 1.0, 0.33), // Mid Blue
                ColorEnd = new ColorHSL(0.66, 1.0, 0.0) // Black
            };
            ComputeLinearRanges(slightSlopeRange, moderateSlopeRange, extremeSlopeRange);
        }

    }
}
