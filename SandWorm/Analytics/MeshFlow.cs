using System.Collections.Generic;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace SandWorm.Analytics
{
    class MeshFlow
    {
        public static double[] waterHead;
        public static Point3d[] waterElevationPoints;
        private static double[] runoffCoefficients;
        private static double rain = 1;
        public static int[] flowDirections;
        private static double[] waterAmounts;

        private const double flowVelocity = 2;
        private const double deltaX = 1;
        private const double deltaY = 1;
        private const double deltaXY = 0.7; // 1 / sqrt(2)

        public static void CalculateWaterHeadArray(Point3d[] pointArray, double[] elevationsArray, int xStride, int yStride, bool simulateFlood)
        {
            if(runoffCoefficients == null)
            {
                runoffCoefficients = new double[elevationsArray.Length];
                for (int i = 0; i < runoffCoefficients.Length; i++) // Populate array with arbitrary runoff values. Ideally, this should be provided by users through UI 
                    runoffCoefficients[i] = 0.4;
            }

            if (waterHead == null)
                waterHead = new double[elevationsArray.Length];

            if (waterElevationPoints == null)
            {
                waterElevationPoints = new Point3d[xStride * yStride];
                Parallel.For(0, elevationsArray.Length, i =>
                {
                    waterElevationPoints[i].X = pointArray[i].X;
                    waterElevationPoints[i].Y = pointArray[i].Y;
                });
            }

            if (flowDirections == null)
                flowDirections = new int[xStride * yStride];

            if (simulateFlood) // Distribute precipitation equally
                for (int i = 0; i < waterHead.Length; i++)
                    waterHead[i] += rain * runoffCoefficients[i];
                

            // Borders are a water sink            
            for (int i = 0; i < xStride - 1; i++) // Bottom border
                waterHead[i] = 0;

            for (int i = (yStride - 1) * xStride; i < yStride * xStride; i++) // Top border
                waterHead[i] = 0;                

            for (int i = xStride; i < (yStride - 1) * xStride; i += xStride) // Left border
                waterHead[i] = 0;

            for (int i = xStride - 1; i < (yStride - 1) * xStride; i += xStride) // Right border
                waterHead[i] = 0;

            waterAmounts = new double[xStride * yStride];

            //for (int rows = 1; rows < yStride - 1; rows++)
            Parallel.For(1, yStride - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 1; columns < xStride - 1; columns++)             // Iterate over x dimension
                {
                    int i = rows * xStride + columns;
                    if (waterHead[i] == 0)
                    {
                        flowDirections[i] = i;
                        continue;
                    }
                        
                    int h = (rows - 1) * xStride + columns;
                    int j = (rows + 1) * xStride + columns;

                    List<int> indices = new List<int>() { h - 1, h, h + 1, i + 1, j + 1, j, j - 1, i - 1 }; // SW, S, SE, E, NE, N, NW, W
                    List<double> deltas = new List<double>() { deltaXY, deltaY, deltaXY, deltaX, deltaXY, deltaY, deltaXY, deltaX };

                    SetFlowDirection(elevationsArray, flowDirections, i, indices, deltas);
                }
            });

            //for (int rows = 1; rows < yStride - 1; rows++)
            Parallel.For(0, yStride - 1, rows =>
            {
                for (int columns = 1; columns < xStride - 1; columns++)
                {
                    int i = rows * xStride + columns;
                    DistributeWater(flowDirections, i);
                }
            });

            Parallel.For(0, elevationsArray.Length, i =>
            {
                if (waterHead[i] > 0)
                    waterElevationPoints[i].Z = pointArray[i].Z + waterHead[i];
                else
                    waterElevationPoints[i].Z = pointArray[i].Z - 1; // Hide water mesh under terrain
            });          
        }


        private static void SetFlowDirection(double[] elevationsArray, int[] flowDirections, int currentIndex, List<int> indices, List<double> deltas)
        {
            double waterLevel = elevationsArray[currentIndex] - waterHead[currentIndex];
            double maxSlope = 0;
            double maxDeltaZ = 0;
            int maxIndex = currentIndex;

            for (int i = 0; i < indices.Count; i++)
            {
                double _deltaZ = waterLevel - elevationsArray[indices[i]] + waterHead[indices[i]]; // Working on inverted elevation values 
                double _slope = _deltaZ * deltas[i];
                if (_slope < maxSlope) // Again, inverted elevation values
                {
                    maxSlope = _slope;
                    maxDeltaZ = _deltaZ;
                    maxIndex = indices[i];
                }
            }

            double _waterAmountHalved = maxDeltaZ * -0.5; // Divide by -2 to split the water equally. Negative number is due to inverted elevation table coming from the sensor
            double waterAmount = _waterAmountHalved < waterHead[currentIndex] ? _waterAmountHalved : waterHead[currentIndex]; // Clamp to the amount of water a cell actually contains
            
            waterAmounts[currentIndex] = waterAmount;
            flowDirections[currentIndex] = maxIndex;
        }

        private static void DistributeWater(int[] flowDirections, int currentIndex)
        {
            int destination = flowDirections[currentIndex];

            if (waterHead[currentIndex] == 0 || destination == currentIndex)
                return;

            // Clamp water flow to max value defined by flow velocity
            double waterFlow = waterAmounts[currentIndex] < flowVelocity ? waterAmounts[currentIndex] : flowVelocity;

            waterHead[currentIndex] -= waterFlow;
            waterHead[destination] += waterFlow;
        }
    }
}
