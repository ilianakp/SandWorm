using System;
using System.Threading.Tasks;
using Rhino.Geometry;

using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

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

        private const double flowVelocity = 2; // Set max amount of water cells can exchange with each other in one iteration

        // GPU variables
        public static Context context;
        public static Accelerator accelerator;
        public static MemoryBuffer1D<int, Stride1D.Dense> _d_flowDirections;
        public static MemoryBuffer1D<double, Stride1D.Dense> _d_waterAmounts;
        public static MemoryBuffer1D<double, Stride1D.Dense> _d_elevationsArray;
        public static MemoryBuffer1D<double, Stride1D.Dense> _d_waterHead;
        private static Action<Index1D, ArrayView<double>, ArrayView<double>, int, int, ArrayView<int>, ArrayView<double>> loadedKernel;

        public static void CalculateWaterHeadArray(Point3d[] pointArray, double[] elevationsArray, int xStride, int yStride, bool simulateFlood)
        {
            #region Initialize

            if (waterHead == null)
            {
                waterHead = new double[elevationsArray.Length];
                waterAmounts = new double[elevationsArray.Length];
                flowDirections = new int[elevationsArray.Length];

                waterElevationPoints = new Point3d[elevationsArray.Length];
                Parallel.For(0, elevationsArray.Length, i =>
                {
                    waterElevationPoints[i].X = pointArray[i].X;
                    waterElevationPoints[i].Y = pointArray[i].Y;
                });

                runoffCoefficients = new double[elevationsArray.Length];
                for (int i = 0; i < runoffCoefficients.Length; i++) // Populate array with arbitrary runoff values. Ideally, this should be provided by users through UI 
                    runoffCoefficients[i] = 0.8;
            }

            if (context == null || context.IsDisposed)
            {
                context = Context.Create(builder => builder.AllAccelerators());

                if (context.GetCudaDevices().Count > 0) // prefer NVIDIA
                    accelerator = context.GetCudaDevice(0).CreateAccelerator(context);
                else // let ILGPU decide
                    accelerator = context.GetPreferredDevice(false).CreateAccelerator(context);

                Rhino.RhinoApp.WriteLine($"Calculations accelerated with {accelerator.Name}.");

                // allocate memory buffers on the GPU
                _d_elevationsArray = accelerator.Allocate1D(elevationsArray);
                _d_waterHead = accelerator.Allocate1D(waterHead);

                _d_flowDirections = accelerator.Allocate1D(flowDirections);
                _d_waterAmounts = accelerator.Allocate1D(waterAmounts);

                // precompile the kernel
                loadedKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>, int, int, ArrayView<int>, ArrayView<double>>(FlowDirectionKernel);
            }
            #endregion

            if (simulateFlood) // Distribute precipitation equally
                Parallel.For(0, waterHead.Length, i =>
                {
                    waterHead[i] += rain;
                });

            DrainBorders(xStride, yStride);

            // copy data from CPU memory space to the GPU
            _d_elevationsArray.CopyFromCPU(elevationsArray);
            _d_waterHead.CopyFromCPU(waterHead);

            // tell the accelerator to start computing the kernel
            loadedKernel(elevationsArray.Length, _d_elevationsArray.View, _d_waterHead.View, xStride, yStride, _d_flowDirections.View, _d_waterAmounts.View);
            accelerator.Synchronize();

            // copy output data from the GPU back to the CPU 
            flowDirections = _d_flowDirections.GetAsArray1D();
            waterAmounts = _d_waterAmounts.GetAsArray1D();

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

        private static void FlowDirectionKernel(Index1D i, ArrayView<double> _elevationsArray, ArrayView<double> _waterHead, int _xStride, int _yStride, ArrayView<int> _flowDirections, ArrayView<double> _waterAmounts)
        {
            if (_waterHead[i] == 0)
                _flowDirections[i] = i;
            else
            {
                int h = i - _xStride;
                int j = i + _xStride;

                int[] indices = new int[8] { h - 1, h, h + 1, i + 1, j + 1, j, j - 1, i - 1 }; // SW, S, SE, E, NE, N, NW, W
                double[] deltas = new double[8] { 0.7, 1, 0.7, 1, 0.7, 1, 0.7, 1 }; // deltaXY = 0.7, deltaX & deltaY = 1

                double waterLevel = _elevationsArray[i] - _waterHead[i];
                double maxSlope = 0;
                double maxDeltaZ = 0;
                int maxIndex = i;

                for (int o = 0; o < indices.Length; o++)
                {
                    if (indices[o] >= 0 && indices[o] <= _xStride * _yStride) // Make sure we are not out of bounds
                    {
                        double _deltaZ = waterLevel - _elevationsArray[indices[o]] + _waterHead[indices[o]]; // Working on inverted elevation values 
                        double _slope = _deltaZ * deltas[o];
                        if (_slope < maxSlope) // Again, inverted elevation values
                        {
                            maxSlope = _slope;
                            maxDeltaZ = _deltaZ;
                            maxIndex = indices[o];
                        }
                    }
                }

                double _waterAmountHalved = maxDeltaZ * -0.5; // Divide by -2 to split the water equally. Negative number is due to inverted elevation table coming from the sensor
                double waterAmount = _waterAmountHalved < _waterHead[i] ? _waterAmountHalved : _waterHead[i]; // Clamp to the amount of water a cell actually contains

                _waterAmounts[i] = waterAmount;
                _flowDirections[i] = maxIndex;
            }
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

        private static void DrainBorders(int xStride, int yStride)
        {
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
        }
    }
}
