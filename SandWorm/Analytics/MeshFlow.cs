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
        private static double[] waterHead;
        private static double[] runoffCoefficients;
        private const double rain = 0.1;

        public static void CreateMeshFlow(Point3d[] pointArray, ref Mesh waterMesh, int xStride, int yStride)
        {
            if(runoffCoefficients == null)
            {
                runoffCoefficients = new double[pointArray.Length];
                for (int i = 0; i < runoffCoefficients.Length; i++) // Populate array with arbitrary runoff values. Ideally, this should be provided by users through UI 
                    runoffCoefficients[i] = 0.4;
            }

            if (waterHead == null)
                waterHead = new double[pointArray.Length];


            double deltaX;
            double deltaY;
            double deltaXY;
            List<double> slopes = new List<double>();
            List<int> indices;
            List<double> deltas;
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

            CalculateWaterLevel(currentIndex, pointArray, head, waterLevel);
            CalculateSlopes(slopes, waterLevel, pointArray, indices, deltas);
            DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            /*
            slopes.Add((waterLevel - pointArray[indices[0]].Z - waterHead[indices[0]]) / deltaX); // E Pixel
            slopes.Add((waterLevel - pointArray[indices[1]].Z - waterHead[indices[1]]) / deltaY); // S Pixel
            slopes.Add((waterLevel - pointArray[indices[2]].Z - waterHead[indices[2]]) / deltaXY); // SE Pixel
            */
            #endregion

            #region Last pixel NE
            deltaX = Math.Abs(pointArray[xStride - 2].X - pointArray[xStride - 1].X);
            deltaY = Math.Abs(pointArray[2 * xStride - 1].Y - pointArray[xStride - 1].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slopes.Clear();
            indices = new List<int>() { xStride - 2, 2 * xStride - 1, 2 * xStride - 2 };
            currentIndex = xStride - 1;

            CalculateWaterLevel(currentIndex, pointArray, head, waterLevel);

            slopes.Add((waterLevel - pointArray[indices[0]].Z - waterHead[indices[0]]) / deltaX); // W Pixel
            slopes.Add((waterLevel - pointArray[indices[1]].Z - waterHead[indices[1]]) / deltaY); // S Pixel
            slopes.Add((waterLevel - pointArray[indices[2]].Z - waterHead[indices[2]]) / deltaXY); // SW Pixel

            DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            #endregion

            #region First pixel SW
            deltaX = Math.Abs(pointArray[(yStride - 1) * xStride + 1].X - pointArray[(yStride - 1) * xStride].X);
            deltaY = Math.Abs(pointArray[(yStride - 2) * xStride].Y - pointArray[(yStride - 1) * xStride].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slopes.Clear();
            indices = new List<int>() { (yStride - 1) * xStride + 1 , (yStride - 2) * xStride , (yStride - 2) * xStride + 1 };
            currentIndex = (yStride - 1) * xStride;

            CalculateWaterLevel(currentIndex, pointArray, head, waterLevel);

            slopes.Add((waterLevel - pointArray[indices[0]].Z - waterHead[indices[0]]) / deltaX); // E Pixel
            slopes.Add((waterLevel - pointArray[indices[1]].Z - waterHead[indices[1]]) / deltaY); // N Pixel
            slopes.Add((waterLevel - pointArray[indices[2]].Z - waterHead[indices[2]]) / deltaXY); //NE Pixel

            DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);

            #endregion

            #region Last pixel SE
            deltaX = Math.Abs(pointArray[yStride * xStride - 2].X - pointArray[yStride * xStride - 1].X);
            deltaY = Math.Abs(pointArray[(yStride - 1) * xStride - 1].Y - pointArray[yStride * xStride - 1].Y);
            deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

            slopes.Clear();
            indices = new List<int>() { yStride * xStride - 2, (yStride - 1) * xStride - 1, (yStride - 1) * xStride - 2 };
            currentIndex = yStride * xStride - 1;

            CalculateWaterLevel(currentIndex, pointArray, head, waterLevel);

            slopes.Add((waterLevel - pointArray[indices[0]].Z - waterHead[indices[0]]) / deltaX); // W Pixel
            slopes.Add((waterLevel - pointArray[indices[1]].Z - waterHead[indices[1]]) / deltaY); // N Pixel
            slopes.Add((waterLevel - pointArray[indices[2]].Z - waterHead[indices[2]]) / deltaXY); //NW Pixel

            DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            #endregion

            #region First row
            for (int i = 1; i < xStride - 1; i++)
            {
                deltaX = Math.Abs(pointArray[i - 1].X - pointArray[i].X);
                deltaY = Math.Abs(pointArray[i + xStride].Y - pointArray[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slopes.Clear();
                indices = new List<int>() { i - 1, i + 1, i + xStride - 1 , i + xStride , i + xStride + 1 };
                currentIndex = i;

                CalculateWaterLevel(currentIndex, pointArray, head, waterLevel);

                slopes.Add((waterLevel - pointArray[indices[0]].Z - waterHead[indices[0]]) / deltaX); // W Pixel
                slopes.Add((waterLevel - pointArray[indices[1]].Z - waterHead[indices[1]]) / deltaX); // E Pixel
                slopes.Add((waterLevel - pointArray[indices[2]].Z - waterHead[indices[2]]) / deltaXY); // SW Pixel
                slopes.Add((waterLevel - pointArray[indices[3]].Z - waterHead[indices[3]]) / deltaY); // S Pixel
                slopes.Add((waterLevel - pointArray[indices[4]].Z - waterHead[indices[4]]) / deltaXY); // SE Pixel

                DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            }
            #endregion

            #region Last row
            for (int i = (yStride - 1) * xStride + 1; i < yStride * xStride - 1; i++)
            {
                deltaX = Math.Abs(pointArray[i - 1].X - pointArray[i].X);
                deltaY = Math.Abs(pointArray[i - xStride].Y - pointArray[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slopes.Clear();
                indices = new List<int>() { i - 1, i + 1, i - xStride - 1, i - xStride, i - xStride + 1 };
                currentIndex = i;

                CalculateWaterLevel(currentIndex, pointArray, head, waterLevel);

                slopes.Add((waterLevel - pointArray[indices[0]].Z - waterHead[indices[0]]) / deltaX); // W Pixel
                slopes.Add((waterLevel - pointArray[indices[1]].Z - waterHead[indices[1]]) / deltaX); // E Pixel
                slopes.Add((waterLevel - pointArray[indices[2]].Z - waterHead[indices[2]]) / deltaXY); // NW Pixel
                slopes.Add((waterLevel - pointArray[indices[3]].Z - waterHead[indices[3]]) / deltaY); // N Pixel
                slopes.Add((waterLevel - pointArray[indices[4]].Z - waterHead[indices[4]]) / deltaXY); // NE Pixel

                DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            }
            #endregion

            #region First column
            for (int i = xStride; i < (yStride - 1) * xStride; i += xStride)
            {
                deltaX = Math.Abs(pointArray[i - xStride].X - pointArray[i].X);
                deltaY = Math.Abs(pointArray[i + 1].Y - pointArray[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slopes.Clear();
                indices = new List<int>() { i - xStride, i + xStride, i - xStride + 1, i + 1, i + xStride + 1 };
                currentIndex = i;

                CalculateWaterLevel(currentIndex, pointArray, head, waterLevel);

                slopes.Add((waterLevel - pointArray[indices[0]].Z - waterHead[indices[0]]) / deltaY); // N Pixel
                slopes.Add((waterLevel - pointArray[indices[1]].Z - waterHead[indices[1]]) / deltaY); // S Pixel
                slopes.Add((waterLevel - pointArray[indices[2]].Z - waterHead[indices[2]]) / deltaXY); // NE Pixel
                slopes.Add((waterLevel - pointArray[indices[3]].Z - waterHead[indices[3]]) / deltaX); // E Pixel
                slopes.Add((waterLevel - pointArray[indices[4]].Z - waterHead[indices[4]]) / deltaXY); // SE Pixel

                DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            }
            #endregion

            #region Last column
            for (int i = 2 * xStride - 1; i < yStride * xStride - 1; i += xStride)
            {
                deltaX = Math.Abs(pointArray[i - xStride].X - pointArray[i].X);
                deltaY = Math.Abs(pointArray[i - 1].Y - pointArray[i].Y);
                deltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                slopes.Clear();
                indices = new List<int>() { i - xStride, i + xStride, i - xStride - 1, i - 1, i + xStride - 1 };
                currentIndex = i;

                CalculateWaterLevel(currentIndex, pointArray, head, waterLevel);

                slopes.Add((waterLevel - pointArray[indices[0]].Z - waterHead[indices[0]]) / deltaY); // N Pixel
                slopes.Add((waterLevel - pointArray[indices[1]].Z - waterHead[indices[1]]) / deltaY); // S Pixel
                slopes.Add((waterLevel - pointArray[indices[2]].Z - waterHead[indices[2]]) / deltaXY); // NW Pixel
                slopes.Add((waterLevel - pointArray[indices[3]].Z - waterHead[indices[3]]) / deltaX); // W Pixel
                slopes.Add((waterLevel - pointArray[indices[4]].Z - waterHead[indices[4]]) / deltaXY); // SW Pixel

                DistributeWaterhead(currentIndex, slopes, indices, pointArray, head, waterLevel);
            }
            #endregion

            // rest of the array
            Parallel.For(1, yStride - 1, rows =>         // Iterate over y dimension
            {
                for (int columns = 1; columns < xStride - 1; columns++)             // Iterate over x dimension
                {
                    int h = (rows - 1) * xStride + columns;
                    int i = rows * xStride + columns;
                    int j = (rows + 1) * xStride + columns;

                    List<double> pSlopes = new List<double>(); // Declare local lists for the parallel loop 
                    List<int> pD8 = new List<int>() { h - 1, h, h + 1, i - 1, i + 1, j - 1, j, j + 1 };

                    double pDeltaX = Math.Abs(pointArray[i + 1].X - pointArray[i].X);
                    double pDeltaY = Math.Abs(pointArray[h].Y - pointArray[i].Y);
                    double pdeltaXY = Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));

                    double pHead = 0;
                    double pWaterLevel = 0;

                    CalculateWaterLevel(i, pointArray, pHead, pWaterLevel);

                    pSlopes.Add(((pointArray[i].Z - pointArray[pD8[0]].Z)) / pdeltaXY); //NW pixel
                    pSlopes.Add(((pointArray[i].Z - pointArray[pD8[1]].Z)) / pDeltaY); //N pixel
                    pSlopes.Add(((pointArray[i].Z - pointArray[pD8[2]].Z)) / pdeltaXY); //NE pixel
                    pSlopes.Add(((pointArray[i].Z - pointArray[pD8[3]].Z)) / pDeltaX); //W pixel
                    pSlopes.Add(((pointArray[i].Z - pointArray[pD8[4]].Z)) / pDeltaX); //E pixel
                    pSlopes.Add(((pointArray[i].Z - pointArray[pD8[5]].Z)) / pdeltaXY); //SW pixel
                    pSlopes.Add(((pointArray[i].Z - pointArray[pD8[6]].Z)) / pDeltaY); //S pixel
                    pSlopes.Add(((pointArray[i].Z - pointArray[pD8[7]].Z)) / pdeltaXY); //SE pixel

                    
                }
            });


        }

        private static int? FindMaxSlope(List<double> slopes, List<int> indices)
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

            int? maxIndex = FindMaxSlope(slopes, indices);

            if (maxIndex != null) // Water head at current cell is higher than at least one of the surrounding cells
            {
                int i = indices[(int)maxIndex]; // Index of the lowest cell
                double _waterHeadHalved = (waterLevel - pointArray[i].Z - waterHead[i]) / 2;

                if (_waterHeadHalved >= head) // If elevation difference between cells permits, move the whole water head to the lowest cell
                {
                    waterHead[i] += head;
                    waterHead[currentIndex] -= head;
                }
                else // Split water head equally between cells
                {
                    waterHead[i] += _waterHeadHalved;
                    waterHead[currentIndex] -= _waterHeadHalved;
                }
            } 
            else // Flow to adjacent cells not possible
                waterHead[currentIndex] = head; 
        }
        private static void CalculateWaterLevel(int currentIndex, Point3d[] pointArray, double head, double waterLevel)
        {
            head = rain * runoffCoefficients[currentIndex] + waterHead[currentIndex];
            waterLevel = pointArray[currentIndex].Z + head;
        }
        private static void CalculateSlopes(List<double> slopes, double waterLevel, Point3d[] pointArray, List<int> indices, List<double> deltas)
        {
            slopes.Clear();
            for (int i = 0; i < indices.Count; i++)
                slopes.Add((waterLevel - pointArray[indices[i]].Z - waterHead[indices[i]]) / deltas[i]); // E Pixel
        }
    }
}
