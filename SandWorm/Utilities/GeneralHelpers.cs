﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SandWorm
{
    public static class GeneralHelpers
    {
        public static void HideParameterGeometry(Grasshopper.Kernel.IGH_Param parameter)
        {
            // Casting to IGH_PreviewObject allows access to the 'Hidden' property
            if (parameter.Recipients.Count > 0)
                ((Grasshopper.Kernel.IGH_PreviewObject)parameter).Hidden = true; 
            else
                ((Grasshopper.Kernel.IGH_PreviewObject)parameter).Hidden = false;
        }
        public static void SetupLogging(ref Stopwatch timer, ref List<string> output)
        {
            timer = Stopwatch.StartNew(); // Setup timer used for debugging
            output = new List<string>(); // For the debugging log lines
        }
        public static void LogTiming(ref List<string> output, Stopwatch timer, string eventDescription)
        {
            var logInfo = eventDescription + ": ";
            timer.Stop();
            output.Add(logInfo.PadRight(28, ' ') + timer.ElapsedMilliseconds.ToString() + " ms");
            timer.Restart();
        }

        public static double ConvertDrawingUnits(Rhino.UnitSystem units)
        {
            double unitsMultiplier = 1.0;

            switch (units.ToString())
            {
                case "Kilometers":
                    unitsMultiplier = 0.0001;
                    break;

                case "Meters":
                    unitsMultiplier = 0.001;
                    break;

                case "Decimeters":
                    unitsMultiplier = 0.01;
                    break;

                case "Centimeters":
                    unitsMultiplier = 0.1;
                    break;

                case "Millimeters":
                    unitsMultiplier = 1.0;
                    break;

                case "Inches":
                    unitsMultiplier = 0.0393701;
                    break;

                case "Feet":
                    unitsMultiplier = 0.0328084;
                    break;
            }
            return unitsMultiplier;
        }

        public static int ConvertFPStoMilliseconds(int fps)
        {
            switch (fps)
            {
                default:
                case 0: // Max 
                    return 33;

                case 1: // 15 FPS
                    return 66;

                case 2: // 5 FPS
                    return 200;

                case 3: // 1 FPS
                    return 1000;

                case 4: // 0.2 FPS
                    return 5000;
            }
        }

        public static void SwapLeftRight(Structs.KinectTypes sensorType, double leftColumns, double rightColumns, ref double _left, ref double _right)
        {
            switch (sensorType)
            {
                case Structs.KinectTypes.KinectForWindows:
                    _left = rightColumns;
                    _right = leftColumns;
                    break;

                case Structs.KinectTypes.KinectAzureNear:
                case Structs.KinectTypes.KinectAzureWide:
                    _left = leftColumns;
                    _right = rightColumns;
                    break;
            }
        }

        public static void CreateLabels(Rhino.Geometry.Point3d[] pointArray, ref List<Rhino.Display.Text3d> labels, 
                                        Structs.AnalysisTypes analysisType, double?[] baseMeshElevationPoints,
                                        int xStride, int yStride, int spacing)
        {
            Rhino.Display.Text3d _text;
            double _distanceToTerrain = 5 * SandWormComponent.unitsMultiplier;

            int maxSize = 10;
            double _size = spacing / 5;
            if (_size > maxSize)
                _size = maxSize;

            _size *= SandWormComponent.unitsMultiplier;

            int roundingFactor = CalculateRoundingFactor(SandWormComponent.unitsMultiplier);

            for (int y = 0; y < yStride; y += spacing)       // Iterate over y dimension
                for (int x = spacing; x < xStride; x += spacing)       // Iterate over x dimension
                {
                    int i = y * xStride + x;
                    Rhino.Geometry.Point3d _point = new Rhino.Geometry.Point3d(pointArray[i].X, pointArray[i].Y, pointArray[i].Z + _distanceToTerrain);
                    Rhino.Geometry.Plane _plane = new Rhino.Geometry.Plane(_point, new Rhino.Geometry.Vector3d(0, 0, 1));
                    double _value = 0;
                    
                    switch (analysisType)
                    {
                        case Structs.AnalysisTypes.Elevation:
                            _value = Math.Round(pointArray[i].Z, roundingFactor);
                            break;

                        case Structs.AnalysisTypes.CutFill:
                            if (baseMeshElevationPoints[i] == null)
                                continue;
                            
                            _value = Math.Round(pointArray[i].Z - (double)baseMeshElevationPoints[i], roundingFactor);
                            break;
                    }

                    _text = new Rhino.Display.Text3d($".{_value}", _plane, _size);
                    labels.Add(_text);
                }
        }

        public static int CalculateRoundingFactor(double unitsMultiplier)
        {
            switch (unitsMultiplier)
            {
                case 1: // Millimeters
                    return 0;
                case 0.1: // Centimeters
                case 0.0393701: // Inches
                    return 1;
                case 0.01: // Decimeters
                case 0.0328084: // Feet
                    return 2;
                case 0.001: // Meters
                    return 3;
                case 0.0001: // Kilometers
                    return 4;
                default:
                    return 0;
            }
        }

        public static Grasshopper.Kernel.Types.GH_Line[] ConvertLineToGHLine(List<Rhino.Geometry.Line> rhinoLines)
        {
            Grasshopper.Kernel.Types.GH_Line[] ghLines = new Grasshopper.Kernel.Types.GH_Line[rhinoLines.Count];

            System.Threading.Tasks.Parallel.For(0, rhinoLines.Count, i =>
            { ghLines[i] = new Grasshopper.Kernel.Types.GH_Line(rhinoLines[i]); });

            return ghLines;
        }

        // Multiply two int[] arrays using SIMD instructions
        public static int[] SimdVectorProd(int[] a, int[] b)
        {
            if (a.Length != b.Length) throw new ArgumentException();
            if (a.Length == 0) return Array.Empty<int>();

            int[] result = new int[a.Length];

            // Get a reference to the first value in all 3 arrays
            ref int ra = ref a[0];
            ref int rb = ref b[0];
            ref int rr = ref result[0];
            int length = a.Length;
            int i = 0;


            /* Calculate the maximum offset we can work on with SIMD instructions.
             * Eg. if each SIMD register can hold 4 int values, and our input
             * arrays have 10 values, we can use SIMD instructions to sum the
             * first two groups of 4 integers, leaving the last 2 out. */
            int end = length - Vector<int>.Count;

            for (; i <= end; i += Vector<int>.Count)
            {
                // Get the reference into a and b at the current position
                ref int rai = ref Unsafe.Add(ref ra, i);
                ref int rbi = ref Unsafe.Add(ref rb, i);

                /* Reinterpret those references as Vector<int>, which effectively
                 * means reading a Vector<int> value starting from the memory
                 * locations those two references are pointing to.
                 * The JIT compiler will make sure that our Vector<int>
                 * variables are stored in exactly one SIMD register each.
                 * Once we have them, we can multiply those together, which will
                 * use a single special SIMD instruction in assembly. */

                // va = { a[i], a[i + 1], ..., a[i + Vector<int>.Count - 1] }
                Vector<int> va = Unsafe.As<int, Vector<int>>(ref rai);

                // vb = { b[i], b[i + 1], ..., b[i + Vector<int>.Count - 1] }
                Vector<int> vb = Unsafe.As<int, Vector<int>>(ref rbi);

                /* vr =
                 * {
                 *     a[i] * b[i],
                 *     a[i + 1] * b[i + 1],
                 *     ...,
                 *     a[i + Vector<int>.Count - 1] * b[i + Vector<int>.Count - 1]
                 * } */
                Vector<int> vr = va * vb;

                // Get the reference into the target array
                ref int rri = ref Unsafe.Add(ref rr, i);

                // Store the resulting vector at the right position
                Unsafe.As<int, Vector<int>>(ref rri) = vr;
            }


            // Multiply the remaining values
            for (; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }

            return result;
        }
    }
}
