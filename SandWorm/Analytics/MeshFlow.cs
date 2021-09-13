using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using static SandWorm.Structs;

namespace SandWorm.Analytics
{
    class MeshFlow
    {
        public static double[] waterHead;
        public static Point3d[] waterElevationPoints;
        private static double[] runoffCoefficients;
        private static double rain = 1;
        private static double flowVelocity;
        public static FlowDirections[] flowDirections;

        public static void CalculateWaterHeadArray(Point3d[] pointArray, int xStride, int yStride, bool simulateFlood)
        {
            if(runoffCoefficients == null)
            {
                runoffCoefficients = new double[pointArray.Length];
                for (int i = 0; i < runoffCoefficients.Length; i++) // Populate array with arbitrary runoff values. Ideally, this should be provided by users through UI 
                    runoffCoefficients[i] = 0.4;
            }

            if (waterHead == null)
                waterHead = new double[pointArray.Length];

            if (waterElevationPoints == null)
            {
                waterElevationPoints = new Point3d[xStride * yStride];
                Parallel.For(0, pointArray.Length, i =>
                {
                    waterElevationPoints[i].X = pointArray[i].X;
                    waterElevationPoints[i].Y = pointArray[i].Y;
                });
            }

            if (flowDirections == null)
                flowDirections = new FlowDirections[xStride * yStride];

            if (simulateFlood) // Distribute precipitation equally
                for (int i = 0; i < waterHead.Length; i++)
                    waterHead[i] += rain * runoffCoefficients[i];
                

            // Borders are a water sink            
            for (int i = 0; i < xStride - 1; i++) // Bottom border
            {
                waterHead[i] = 0;
                flowDirections[i] = FlowDirections.None;
            }
                

            for (int i = (yStride - 1) * xStride; i < yStride * xStride; i++) // Top border
            {
                waterHead[i] = 0;
                flowDirections[i] = FlowDirections.None;
            }
                

            for (int i = xStride; i < (yStride - 1) * xStride; i += xStride) // Left border
            {
                waterHead[i] = 0;
                flowDirections[i] = FlowDirections.None;
            }

            for (int i = xStride - 1; i < (yStride - 1) * xStride; i += xStride) // Right border
            {
                waterHead[i] = 0;
                flowDirections[i] = FlowDirections.None;
            }

            
            flowVelocity = 0.2;

            for (int rows = 1; rows < yStride - 1; rows++)
            //Parallel.For(1, yStride - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 1; columns < xStride - 1; columns++)             // Iterate over x dimension
                {
                    int i = rows * xStride + columns;
                    if (waterHead[i] == 0)
                    {
                        flowDirections[i] = FlowDirections.None;
                        continue;
                    }
                        

                    int h = (rows - 1) * xStride + columns;
                    int j = (rows + 1) * xStride + columns;

                    // Declare local variables for the parallel loop 
                    double deltaX = Math.Abs(pointArray[i].X - pointArray[i + 1].X);
                    double deltaY = Math.Abs(pointArray[i].Y - pointArray[h].Y);
                    double deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                    List<int> indices = new List<int>() { h - 1, h, h + 1, i + 1, j + 1, j, j - 1, i - 1 }; // SW, S, SE, E, NE, N, NW, W
                    List<double> deltas = new List<double>() { deltaXY, deltaY, deltaXY, deltaX, deltaXY, deltaY, deltaXY, deltaX };

                    SetFlowDirection(pointArray, flowDirections, i, indices, deltas);

                }
            }
            //);

            for (int rows = 1; rows < yStride - 1; rows++)
            {
                for (int columns = 1; columns < xStride - 1; columns++)
                {
                    int i = rows * xStride + columns;
                    DistributeWater(flowDirections, i, xStride);
                }
            }
                

            Parallel.For(0, pointArray.Length, i =>
            {
                if (waterHead[i] > 0)
                    waterElevationPoints[i].Z = pointArray[i].Z + waterHead[i];
                else
                    waterElevationPoints[i].Z = pointArray[i].Z - 1; // Hide water mesh under terrain
            });
        }


        private static void SetFlowDirection(Point3d[] pointArray, FlowDirections[] flowDirections, int currentIndex, List<int> indices, List<double> deltas)
        {
            double waterLevel = pointArray[currentIndex].Z + waterHead[currentIndex];
            double maxSlope = 0;
            int maxIndex = 0;

            for (int i = 0; i < indices.Count; i++)
            {
                double _slope = (waterLevel - (pointArray[indices[i]].Z + waterHead[indices[i]])) / deltas[i];
                if (_slope > maxSlope)
                {
                    maxSlope = _slope;
                    maxIndex = i;
                }
            }

            if (maxSlope == 0) // All neighboring cells are higher than current
                flowDirections[currentIndex] = FlowDirections.None;
            else
                flowDirections[currentIndex] = (FlowDirections)maxIndex;
        }

        private static void DistributeWater(FlowDirections[] flowDirections, int currentIndex, int xStride)
        {
            int southIndex = currentIndex - xStride;
            int northIndex = currentIndex + xStride;
            
            // TODO calculate the amount of water, which can be passed down

            if (flowDirections[currentIndex] != FlowDirections.None)
            {
                if (waterHead[currentIndex] > flowVelocity)
                    waterHead[currentIndex] -= flowVelocity;
                else
                    waterHead[currentIndex] = 0;
            }
                

            if (flowDirections[southIndex - 1] == FlowDirections.NE)
                waterHead[currentIndex] += flowVelocity;

            if (flowDirections[southIndex] == FlowDirections.N)
                waterHead[currentIndex] += flowVelocity;

            if (flowDirections[southIndex + 1] == FlowDirections.NW)
                waterHead[currentIndex] += flowVelocity;

            if (flowDirections[currentIndex + 1] == FlowDirections.W)
                waterHead[currentIndex] += flowVelocity;

            if (flowDirections[northIndex + 1] == FlowDirections.SW)
                waterHead[currentIndex] += flowVelocity;

            if (flowDirections[northIndex] == FlowDirections.S)
                waterHead[currentIndex] += flowVelocity;

            if (flowDirections[northIndex - 1] == FlowDirections.SE)
                waterHead[currentIndex] += flowVelocity;

            if (flowDirections[currentIndex - 1] == FlowDirections.E)
                waterHead[currentIndex] += flowVelocity;
        }
    }
}
