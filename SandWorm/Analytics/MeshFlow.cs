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

        public static void CalculateWaterHeadArray(Point3d[] pointArray, int xStride, int yStride, bool makeItRain)
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
                

            // Borders are an absolute water sink
            for (int rows = 0; rows < 1 | rows > yStride - 1; rows++)
                for (int columns = 0; columns < 1 | columns > xStride - 1; columns++)
                    waterHead[rows * xStride + columns] = 0;


            for (int rows = 1; rows < yStride - 1; rows++)
            //Parallel.For(1, yStride - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 1; columns < xStride - 1; columns++)             // Iterate over x dimension
                {
                    int h = (rows - 1) * xStride + columns;
                    int i = rows * xStride + columns;
                    int j = (rows + 1) * xStride + columns;

                    // Declare local variables for the parallel loop 
                    double deltaX = Math.Abs(pointArray[i].X - pointArray[i + 1].X);
                    double deltaY = Math.Abs(pointArray[i].Y - pointArray[h].Y);
                    double deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                    List<double> slopes = new List<double>();
                    List<int> indices = new List<int>() { h - 1, h, h + 1, i - 1, i + 1, j - 1, j, j + 1 };
                    List<double> deltas = new List<double>() { deltaXY, deltaY, deltaXY, deltaX, deltaX, deltaXY, deltaY, deltaXY };
                    double rain = 0;

                    if (makeItRain)
                        rain = 1;

                    double pWaterLevel = CalculateWaterLevel(i, pointArray, rain, out double pHead);
                    if (pHead <= 0)
                    {
                        waterHead[i] = 0;
                        continue;
                    }
                    CalculateSlopes(slopes, pWaterLevel, pointArray, indices, deltas);
                    DistributeWaterhead(i, slopes, indices, pointArray, pHead, pWaterLevel);
                }
            }
            //);

           
            Parallel.For(0, pointArray.Length, i =>
            {
                if (waterHead[i] > 0)
                    waterElevationPoints[i].Z = pointArray[i].Z + waterHead[i];
                else
                    waterElevationPoints[i].Z = pointArray[i].Z - 1; // Hide water mesh under terrain
            });
        }

        private static double CalculateWaterLevel(int currentIndex, Point3d[] pointArray, double rain, out double head)
        {
            head = rain * runoffCoefficients[currentIndex] + waterHead[currentIndex];
            return pointArray[currentIndex].Z + head;
        }
        private static void CalculateSlopes(List<double> slopes, double waterLevel, Point3d[] pointArray, List<int> indices, List<double> deltas)
        {
            slopes.Clear();
            for (int i = 0; i < indices.Count; i++)
                slopes.Add((waterLevel - pointArray[indices[i]].Z - waterHead[indices[i]]) / deltas[i]); 
        }
        private static int? FindMaxSlopeIndex(List<double> slopes, List<int> indices)
        {
            double maxSlope = 0;
            int? maxIndex = null;

            for (int i = 0; i < slopes.Count; i++)
                if(slopes[i] > maxSlope)
                {
                    maxSlope = slopes[i];
                    maxIndex = indices[i];
                }

            return maxIndex;
        }
        private static void DistributeWaterhead(int currentIndex, List<double> slopes, List<int> indices, Point3d[] pointArray, double head, double waterLevel)
        {
            int? maxIndex = FindMaxSlopeIndex(slopes, indices);

            if (maxIndex != null) // Water head at current cell is higher than at least one of the surrounding cells
            {

                int i = (int)maxIndex; // Index of the lowest cell
                double waterHeadHalved = (waterLevel - pointArray[i].Z - waterHead[i]) / 2;

                if (waterHeadHalved >= head) // If elevation difference between cells permits, move the whole water head to the lowest cell
                {
                    waterHead[i] += head;
                    waterHead[currentIndex] = 0;
                }
                else // Split water head equally between cells
                {
                    waterHead[i] += waterHeadHalved;
                    waterHead[currentIndex] = head - waterHeadHalved;
                }
                
            }
            else // Flow to adjacent cells not possible
                waterHead[currentIndex] = head;
        }
    }
}
