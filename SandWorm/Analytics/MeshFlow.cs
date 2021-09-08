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
        private static double rain = 0.00001;

        public static void CalculateWaterHeadArray(Point3d[] pointArray, int xStride, int yStride)
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
                

            double deltaX;
            double deltaY;
            double deltaXY;
            
            List<int> indices;
            List<double> deltas;
            List<double> slopes = new List<double>();

            int currentIndex;
            double head = 0;
            double waterLevel = 0;

            #region First pixel NW
            deltaX = Math.Abs(pointArray[1].X - pointArray[0].X);
            deltaY = Math.Abs(pointArray[xStride].Y - pointArray[0].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            indices = new List<int>(){ 1, xStride, xStride + 1 };
            deltas = new List<double>() { deltaX, deltaY, deltaXY};
            currentIndex = 0;

            waterLevel = CalculateWaterLevel(currentIndex, pointArray, out head);
            CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
            DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            #endregion

            #region Last pixel NE
            deltaX = Math.Abs(pointArray[xStride - 2].X - pointArray[xStride - 1].X);
            deltaY = Math.Abs(pointArray[2 * xStride - 1].Y - pointArray[xStride - 1].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            indices = new List<int>() { xStride - 2, 2 * xStride - 1, 2 * xStride - 2 };
            deltas = new List<double>() { deltaX, deltaY, deltaXY };
            currentIndex = xStride - 1;

            waterLevel = CalculateWaterLevel(currentIndex, pointArray, out head);
            CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
            DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            #endregion

            #region First pixel SW
            deltaX = Math.Abs(pointArray[(yStride - 1) * xStride + 1].X - pointArray[(yStride - 1) * xStride].X);
            deltaY = Math.Abs(pointArray[(yStride - 2) * xStride].Y - pointArray[(yStride - 1) * xStride].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            indices = new List<int>() { (yStride - 1) * xStride + 1 , (yStride - 2) * xStride , (yStride - 2) * xStride + 1 };
            deltas = new List<double>() { deltaX, deltaY, deltaXY };
            currentIndex = (yStride - 1) * xStride;

            waterLevel = CalculateWaterLevel(currentIndex, pointArray, out head);
            CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
            DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            #endregion

            #region Last pixel SE
            deltaX = Math.Abs(pointArray[yStride * xStride - 2].X - pointArray[yStride * xStride - 1].X);
            deltaY = Math.Abs(pointArray[(yStride - 1) * xStride - 1].Y - pointArray[yStride * xStride - 1].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            indices = new List<int>() { yStride * xStride - 2, (yStride - 1) * xStride - 1, (yStride - 1) * xStride - 2 };
            deltas = new List<double>() { deltaX, deltaY, deltaXY };
            currentIndex = yStride * xStride - 1;

            waterLevel = CalculateWaterLevel(currentIndex, pointArray, out head);
            CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
            DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            #endregion

            #region First row
            for (int i = 1; i < xStride - 1; i++)
            {
                deltaX = Math.Abs(pointArray[i - 1].X - pointArray[i].X);
                deltaY = Math.Abs(pointArray[i + xStride].Y - pointArray[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                indices = new List<int>() { i - 1, i + 1, i + xStride - 1 , i + xStride , i + xStride + 1 };
                deltas = new List<double>() { deltaX, deltaX, deltaXY, deltaY, deltaXY};
                currentIndex = i;

                waterLevel = CalculateWaterLevel(currentIndex, pointArray, out head);
                CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
                DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            }
            #endregion

            #region Last row
            for (int i = (yStride - 1) * xStride + 1; i < yStride * xStride - 1; i++)
            {
                deltaX = Math.Abs(pointArray[i - 1].X - pointArray[i].X);
                deltaY = Math.Abs(pointArray[i - xStride].Y - pointArray[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                indices = new List<int>() { i - 1, i + 1, i - xStride - 1, i - xStride, i - xStride + 1 };
                deltas = new List<double>() { deltaX, deltaX, deltaXY, deltaY, deltaXY };
                currentIndex = i;

                waterLevel = CalculateWaterLevel(currentIndex, pointArray, out head);
                CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
                DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            }
            #endregion

            #region First column
            for (int i = xStride; i < (yStride - 1) * xStride; i += xStride)
            {
                deltaX = Math.Abs(pointArray[i].X - pointArray[i + 1].X);
                deltaY = Math.Abs(pointArray[i].Y - pointArray[i - xStride].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                indices = new List<int>() { i - xStride, i + xStride, i - xStride + 1, i + 1, i + xStride + 1 };
                deltas = new List<double>() { deltaY, deltaY, deltaXY, deltaX, deltaXY };
                currentIndex = i;

                waterLevel = CalculateWaterLevel(currentIndex, pointArray, out head);
                CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
                DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            }
            #endregion

            #region Last column
            for (int i = 2 * xStride - 1; i < yStride * xStride - 1; i += xStride)
            {
                deltaX = Math.Abs(pointArray[i].X - pointArray[i - 1].X);
                deltaY = Math.Abs(pointArray[i].Y - pointArray[i - xStride].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                indices = new List<int>() { i - xStride, i + xStride, i - xStride - 1, i - 1, i + xStride - 1 };
                deltas = new List<double>() { deltaY, deltaY, deltaXY, deltaX, deltaXY };
                currentIndex = i;

                waterLevel = CalculateWaterLevel(currentIndex, pointArray, out head);
                CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
                DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            }
            #endregion

            #region Rest of the array
            for (int rows = 1; rows < yStride - 1; rows++)
            //Parallel.For(1, yStride - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 1; columns < xStride - 1; columns++)             // Iterate over x dimension
                {
                    int h = (rows - 1) * xStride + columns;
                    int i = rows * xStride + columns;
                    int j = (rows + 1) * xStride + columns;

                    // Declare local variables for the parallel loop 
                    double pDeltaX = Math.Abs(pointArray[i].X - pointArray[i + 1].X);
                    double pDeltaY = Math.Abs(pointArray[i].Y - pointArray[h].Y);
                    double pDeltaXY = Math.Sqrt(Math.Pow(pDeltaX, 2) + Math.Pow(pDeltaY, 2));

                    List<double> pSlopes = new List<double>();
                    List<int> pIndices = new List<int>() { h - 1, h, h + 1, i - 1, i + 1, j - 1, j, j + 1 };
                    List<double> pDeltas = new List<double>() { pDeltaXY, pDeltaY, pDeltaXY, pDeltaX, pDeltaX, pDeltaXY, pDeltaY, pDeltaXY };

                    double pHead = 0;
                    double pWaterLevel = 0;

                    pWaterLevel = CalculateWaterLevel(i, pointArray, out pHead);
                    CalculateSlopes(pSlopes, pWaterLevel, pointArray, pIndices, pDeltas);
                    DistributeWaterhead(i, pSlopes, pIndices, pointArray, pHead, pWaterLevel);
                }
            }
            //);
            #endregion
           
            Parallel.For(0, pointArray.Length, i =>
            {
                if (waterHead[i] > 0)
                    waterElevationPoints[i].Z = pointArray[i].Z + waterHead[i];
                else
                    waterElevationPoints[i].Z = pointArray[i].Z - 1;
            });
        }

        private static double CalculateWaterLevel(int currentIndex, Point3d[] pointArray, out double head)
        {
            head = 1 * runoffCoefficients[currentIndex] + waterHead[currentIndex];
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
