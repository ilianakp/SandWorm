using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public static double[] velocity;

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

            if(velocity == null)
                 velocity = new double[xStride * yStride];

            if (simulateFlood) // Distribute precipitation equally
                for (int i = 0; i < waterHead.Length; i++)
                    waterHead[i] += rain * runoffCoefficients[i];
                

            // Borders are a water sink            
            for (int i = 0; i < xStride - 1; i++)
                waterHead[i] = 0;

            for (int i = (yStride - 1) * xStride; i < yStride * xStride; i++)
                waterHead[i] = 0;

            for (int i = xStride; i < (yStride - 1) * xStride; i += xStride)
                waterHead[i] = 0;

            for (int i = xStride - 1; i < (yStride - 1) * xStride; i += xStride)
                waterHead[i] = 0;

            double[] waterHeadBuffer = new double[xStride * yStride];
            
            double dt = 0.05;
            double c = 5;

            for (int rows = 1; rows < yStride - 1; rows++)
            //Parallel.For(1, yStride - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 1; columns < xStride - 1; columns++)             // Iterate over x dimension
                {
                    int h = (rows - 1) * xStride + columns;
                    int i = rows * xStride + columns;
                    int j = (rows + 1) * xStride + columns;

                    if (waterHead[i] == 0)
                        continue;

                    // Declare local variables for the parallel loop 
                    double deltaX = Math.Abs(pointArray[i].X - pointArray[i + 1].X);
                    //double deltaY = Math.Abs(pointArray[i].Y - pointArray[h].Y);
                    //double deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                    List<int> indices = new List<int>() { h, i - 1, i + 1, j};

                    // U - water head
                    // h - deltaX
                    // v - deltaU
                    // c - constant speed
                    // dt - time of simulation


                    double force = Math.Pow(c, 2) * ((pointArray[h].Z + waterHead[h] + pointArray[j].Z + waterHead[j] + pointArray[i - 1].Z + waterHead[i - 1] + pointArray[i + 1].Z + waterHead[i + 1]) - (4 * (pointArray[i].Z + waterHead[i]))) / Math.Pow(deltaX, 2);
                    velocity[i] += 0.99 * force * dt;
                    waterHeadBuffer[i] = waterHead[i] + (velocity[i] * dt);

                    //Dictionary<int, double> indexSlopePairs = CalculateSlopes(pointArray, i, indices, deltas);
                    //DistributeWaterhead(pointArray, indexSlopePairs, i);
                }
            }
            //);

            Parallel.For(0, pointArray.Length, i =>
            {
                waterHead[i] = waterHeadBuffer[i];
            });

            Parallel.For(0, pointArray.Length, i =>
            {
                if (waterHead[i] > 0)
                    waterElevationPoints[i].Z = pointArray[i].Z + waterHead[i];
                else
                    waterElevationPoints[i].Z = pointArray[i].Z - 1; // Hide water mesh under terrain
            });
        }


        private static Dictionary<int, double> CalculateSlopes(Point3d[] pointArray, int currentIndex, List<int> indices, List<double> deltas)
        {
            Dictionary<int, double> indexSlopePairs = new Dictionary<int, double>();
            double waterLevel = pointArray[currentIndex].Z + waterHead[currentIndex];

            for (int i = 0; i < indices.Count; i++)
            {
                double _slope = (waterLevel - (pointArray[indices[i]].Z + waterHead[indices[i]])) / deltas[i];
                if (_slope > 0)
                    indexSlopePairs.Add(indices[i], _slope);
            }
            return indexSlopePairs;
        }

        private static void DistributeWaterhead(Point3d[] pointArray, Dictionary<int, double> indexSlopePairs, int currentIndex)
        {
            if (indexSlopePairs.Count == 0) // All neighboring cells are higher than current
                return;

            foreach (var indexSlope in indexSlopePairs.OrderByDescending(key => key.Value)) // Iterate through slopes list in descending order
            {
                if (waterHead[currentIndex] == 0)
                    break;

                double waterHeadHalved = (pointArray[currentIndex].Z + waterHead[currentIndex] - (pointArray[indexSlope.Key].Z + waterHead[indexSlope.Key])) / 2;
                
                if (waterHeadHalved <= 0)
                    continue;
                if (waterHeadHalved >= waterHead[currentIndex]) // If elevation difference between cells permits, move the whole water head to the lowest cell
                {
                    waterHead[indexSlope.Key] += waterHead[currentIndex];
                    waterHead[currentIndex] = 0;
                }
                else // Split water head equally between cells
                {
                    waterHead[currentIndex] -= waterHeadHalved;
                    waterHead[indexSlope.Key] += waterHeadHalved;
                }
            }
        }
    }
}
