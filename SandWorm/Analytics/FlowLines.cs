using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace SandWorm
{
    public class FlowLine
    {
        private int zDistance = 3;
        public int Endvertex { get; set; }
        public Polyline Polyline { get; set; }
        public int Inactive { get; set; }

        public FlowLine(int startVertex, ref Point3d[] points)
        {
            Endvertex = startVertex;
            Polyline = new Polyline();
            Inactive = 0;
            Point3d _pt = new Point3d(points[Endvertex].X, points[Endvertex].Y, points[Endvertex].Z + zDistance); // Raise the polyline slightly above terrain for better visibility
            Polyline.Add(_pt);
        }

        public void Grow(ref Point3d[] points, int xStride)
        {
            int _previousEndvertex = Endvertex;
            findNextPoint(ref points, xStride);
            if (Endvertex != _previousEndvertex)
            {
                Point3d _pt = new Point3d(points[Endvertex].X, points[Endvertex].Y, points[Endvertex].Z + zDistance);
                Polyline.Add(_pt);
                Inactive = 0;
            }
            else
                Inactive++;
        }

        public void Shrink()
        {
            Polyline.RemoveAt(0);
        }

        private void findNextPoint(ref Point3d[] pointArray, int xStride)
        {
            double maxDistance = 500; // Necessary for checks around the mesh borders

            int maxSlopeIndex = Endvertex;
            double maxSlope = 0.0;
            double _slope = 0.0;
            double deltaX = 0.0;
            double deltaY = 0.0;

            int i = Endvertex;

            int _sw = i - xStride - 1;//SW pixel
            int _s = i - xStride; //S pixel
            int _se = i - xStride + 1; //SE pixel
            int _w = i - 1; //W pixel
            int _e = i + 1; //E pixel
            int _nw = i + xStride - 1; //NW pixel
            int _n = i + xStride; //N pixel
            int _ne = i + xStride + 1; //NE pixel

            if (_sw >= 0)
            {
                deltaX = pointArray[i].X - pointArray[_sw].X;
                deltaY = pointArray[i].Y - pointArray[_sw].Y;

                if (Math.Abs(deltaX) < maxDistance)
                {
                    _slope = (pointArray[i].Z - pointArray[_sw].Z) / Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));
                    CheckForMaxSlope(_slope, ref maxSlope, _sw, ref maxSlopeIndex);
                }
            }

            if (_s >= 0)
            {
                deltaY = pointArray[i].Y - pointArray[_s].Y;

                _slope = (pointArray[i].Z - pointArray[_s].Z) / deltaY;
                CheckForMaxSlope(_slope, ref maxSlope, _s, ref maxSlopeIndex);
            }

            if (_se >= 0)
            {
                deltaX = pointArray[i].X - pointArray[_se].X;
                deltaY = pointArray[i].Y - pointArray[_se].Y;

                if (Math.Abs(deltaX) < maxDistance)
                {
                    _slope = (pointArray[i].Z - pointArray[_se].Z) / Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));
                    CheckForMaxSlope(_slope, ref maxSlope, _se, ref maxSlopeIndex);
                }
            }

            if (_w >= 0)
            {
                deltaX = pointArray[i].X - pointArray[_w].X;

                if (Math.Abs(deltaX) < maxDistance)
                {
                    _slope = (pointArray[i].Z - pointArray[_w].Z) / deltaX;
                    CheckForMaxSlope(_slope, ref maxSlope, _w, ref maxSlopeIndex);
                }
            }

            if (_e <= pointArray.Length - 1)
            {
                deltaX = pointArray[i].X - pointArray[_e].X;

                if (Math.Abs(deltaX) < maxDistance)
                {
                    _slope = (pointArray[i].Z - pointArray[_e].Z) / deltaX;
                    CheckForMaxSlope(_slope, ref maxSlope, _e, ref maxSlopeIndex);
                }
            }

            if (_nw <= pointArray.Length - 1)
            {
                deltaX = pointArray[i].X - pointArray[_nw].X;
                deltaY = pointArray[i].Y - pointArray[_nw].Y;

                if (Math.Abs(deltaX) < maxDistance)
                {
                    _slope = (pointArray[i].Z - pointArray[_nw].Z) / Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));
                    CheckForMaxSlope(_slope, ref maxSlope, _nw, ref maxSlopeIndex);
                }
            }

            if (_n <= pointArray.Length - 1)
            {
                deltaY = pointArray[i].Y - pointArray[_n].Y;

                _slope = (pointArray[i].Z - pointArray[_n].Z) / deltaY;
                CheckForMaxSlope(_slope, ref maxSlope, _n, ref maxSlopeIndex);
            }

            if (_ne <= pointArray.Length - 1)
            {
                deltaX = pointArray[i].X - pointArray[_ne].X;
                deltaY = pointArray[i].Y - pointArray[_ne].Y;

                if (Math.Abs(deltaX) < maxDistance)
                {
                    _slope = (pointArray[i].Z - pointArray[_ne].Z) / Math.Sqrt(Math.Pow(deltaX, 2) + Math.Pow(deltaY, 2));
                    CheckForMaxSlope(_slope, ref maxSlope, _ne, ref maxSlopeIndex);
                }
            }

            if (maxSlope > 0)
                Endvertex = maxSlopeIndex;
        }

        private void CheckForMaxSlope(double currentSlope, ref double maxSlope, int currentIndex, ref int maxSlopeIndex)
        {
            if (currentSlope > maxSlope)
            {
                maxSlope = currentSlope;
                maxSlopeIndex = currentIndex;
            }
        }

        public static void DistributeFlowLines(Point3d[] pointArray, ref List<FlowLine> flowLines, int xStride, int yStride, int spacing)
        {
            for (int y = 0; y < yStride; y += spacing)       // Iterate over y dimension
                for (int x = spacing; x < xStride; x += spacing)       // Iterate over x dimension
                {
                    int i = y * xStride + x;
                    flowLines.Add(new FlowLine(i, ref pointArray));
                }
        }

        public static void DistributeRandomRaindrops(Point3d[] pointArray, ref List<FlowLine> flowLines, int spacing)
        {
            Random random = new Random();
            int flowLinesCount = pointArray.Length / (spacing * 10); // Arbitrary division by 10 to reduce the amount of flowlines
            int pointsCount = pointArray.Length - 1;

            for (int i = 0; i < flowLinesCount; i++)
                flowLines.Add(new FlowLine(random.Next(pointsCount), ref pointArray));
        }

        public static void GrowAndRemoveFlowlines(ref Point3d[] pointArray, ref List<FlowLine> flowLines, int spacing, double maxLength)
        {
            List<int> deadIndices = new List<int>();

            for (int i = 0; i < flowLines.Count; i++)
            {
                if (flowLines[i].Polyline.Length > maxLength)
                    flowLines[i].Shrink();

                if (flowLines[i].Inactive < 5)
                    flowLines[i].Grow(ref pointArray, spacing);
                else if (flowLines[i].Polyline.Length > 0)
                    flowLines[i].Shrink();
                else
                    deadIndices.Add(i); // Mark polylines for removal if they were stuck for more than 5 ticks 
            }

            for (int i = deadIndices.Count - 1; i > 0; i--)
                flowLines.RemoveAt(i);
        }
    }
}
