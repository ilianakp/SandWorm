using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rhino.Geometry;

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using System.IO;

namespace SandWorm.Analytics
{
    class MeshFlow
    {
        public static double[] waterHead;
        public static Point3d[] waterElevationPoints;
        public static double[] runoffCoefficients;
        private static double rain = 1;
        public static int[] flowDirections;
        private static double[] waterAmounts;

        private const double flowVelocity = 2;
        private const double deltaX = 1;
        private const double deltaY = 1;
        private const double deltaXY = 0.7; // 1 / sqrt(2)

        private static Context context;
        private static Accelerator accelerator;

        static void Kernel(Index1D i, ArrayView<int> data, ArrayView<int> output)
        {
            output[i] = i * 2;
        }

        public static void CalculateWaterHeadArray(Point3d[] pointArray, double[] elevationsArray, int xStride, int yStride, bool simulateFlood)
        {
            if (runoffCoefficients == null)
            {
                runoffCoefficients = new double[elevationsArray.Length];
                for (int i = 0; i < runoffCoefficients.Length; i++) // Populate array with arbitrary runoff values. Ideally, this should be provided by users through UI 
                    runoffCoefficients[i] = 0.8;
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
                Parallel.For(0, waterHead.Length, i =>
                {
                    waterHead[i] += rain;
                });

            // Borders are a water sink            
            for (int i = 0, e = xStride - 1; i < e; i++) // Bottom border
                waterHead[i] = 0;

            for (int i = (yStride - 1) * xStride, e = yStride * xStride; i < e; i++) // Top border
                waterHead[i] = 0;

            for (int i = xStride, e = (yStride - 1) * xStride; i < e; i += xStride) 
            {
                waterHead[i] = 0; // Left border
                waterHead[i - 1] = 0; // Right border
            }

            waterAmounts = new double[xStride * yStride];



            if (context.IsDisposed)
            {
                context = Context.Create(builder => builder.Cuda());
                accelerator = context.GetPreferredDevice(false)
                                          .CreateAccelerator(context);
            }

            MemoryBuffer1D<int, Stride1D.Dense> deviceData = accelerator.Allocate1D(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            MemoryBuffer1D<int, Stride1D.Dense> deviceOutput = accelerator.Allocate1D<int>(100);

            // precompile the kernel
            Action<Index1D, ArrayView<int>, ArrayView<int>> loadedKernel =
                accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, ArrayView<int>>(Kernel);

            // finish compiling and tell the accelerator to start computing the kernel
            loadedKernel((int)deviceOutput.Length, deviceData.View, deviceOutput.View);

            accelerator.Synchronize();

            // moved output data from the GPU to the CPU for output to console
            int[] hostOutput = deviceOutput.GetAsArray1D();

            /*
             
            deviceOutput.Dispose();
            deviceData.Dispose();
            accelerator.Dispose();
            context.Dispose();
            */


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
                        
                    int h = i - xStride;
                    int j = i + xStride;

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

            // If the cell isn't wet reduce outflow to neighbors by infiltration & evaporation
            if (waterHead[currentIndex] > 0)
                waterHead[destination] += waterFlow * 0.96; // There are always some water losses. This is a somewhat arbitrary factor
            else
                waterHead[destination] += waterFlow * runoffCoefficients[currentIndex]; 
        }
    }
}
