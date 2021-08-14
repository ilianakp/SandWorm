using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Rhino.Geometry;


namespace SandWorm.Analytics
{
    public static class ContoursFromPoints
    {
        public static void GetGeometryForAnalysis(ref List<Line> contourLines, Point3d[] points, int threshold, int trimmedWidth, int trimmedHeight, int multiplier)
        {

            int xd = trimmedWidth;
            int yd = trimmedHeight;

            var ydd = (int)(yd / multiplier);
            var xdd = (int)(xd / multiplier);

            ConcurrentBag<Line> _contourLines = new ConcurrentBag<Line>();

            Parallel.For(1, ydd, y =>       // Iterate over y dimension
            {
                for (int x = 1; x < xdd; x++)       // Iterate over x dimension
                {
                    List<Point4d> intersectionPoints = new List<Point4d>();

                    int i = y * multiplier * xd + (x * multiplier);
                    int j = y * multiplier * xd - (multiplier * xd) + (x * multiplier);

                    // lower left corner -> j - multiplier
                    // lower right corner -> j
                    // upper right corner-> i
                    // upper left corner -> i - multiplier

                    intersectionPoints.AddRange(FindIntersections(points[j - multiplier], points[j], threshold));
                    intersectionPoints.AddRange(FindIntersections(points[j], points[i], threshold));
                    intersectionPoints.AddRange(FindIntersections(points[i], points[i - multiplier], threshold));
                    intersectionPoints.AddRange(FindIntersections(points[i - multiplier], points[j - multiplier], threshold));

                    if (intersectionPoints.Count > 0)
                    {
                        Stack<Point4d> vertexStack = new Stack<Point4d>();

                        for (int a = 0; a < intersectionPoints.Count; a++)
                        {
                            if (vertexStack.Count == 0 || vertexStack.Peek().W == intersectionPoints[a].W) // Add points to the stack if they have the same direction
                                vertexStack.Push(intersectionPoints[a]);
                            else
                            {
                                Point4d _start = vertexStack.Pop();
                                Point4d _end = intersectionPoints[a];

                                if (_start.Z != _end.Z)
                                {
                                    if (a < intersectionPoints.Count - 1)
                                    {
                                        Point4d _next = intersectionPoints[a + 1];

                                        if (_start.Z == _next.Z && _start.W != _next.W)
                                            _end = _next;
                                        else if (vertexStack.Count > 0)
                                            _start = vertexStack.Pop();
                                        else
                                        {
                                            vertexStack.Push(_start);
                                            vertexStack.Push(intersectionPoints[a]);
                                            continue;
                                        }
                                    }
                                }
                                if( _start.Z == _end.Z)
                                    _contourLines.Add(new Line(_start.X, _start.Y, _start.Z, _end.X, _end.Y, _end.Z));
                            }
                        }
                    }
                }
            }); // Parallel for

            contourLines = new List<Line>(_contourLines);
        }


        private static List<Point4d> FindIntersections(Point3d startVertex, Point3d endVertex, int threshold)
        {
            List<Point4d> intersections = new List<Point4d>();
            Point4d _p = new Point4d();
            double deltaZ = Math.Abs(endVertex.Z - startVertex.Z);

            if ((int)startVertex.Z == (int)endVertex.Z || deltaZ == 0) // Discard points with the same integer (e.g. 40.1 & 40.9) as they don't cross an isocurve
                return intersections;

            double ratio = 0.0;

            for (int a = 1; a <= Math.Ceiling(deltaZ); a++)
            {
                if (startVertex.Z < endVertex.Z)
                {
                    _p.Z = Math.Floor(startVertex.Z) + a;
                    ratio = (_p.Z - startVertex.Z) / deltaZ;
                }
                else
                {
                    _p.Z = Math.Ceiling(startVertex.Z) - a;
                    ratio = 1 - ((_p.Z - endVertex.Z) / deltaZ);
                    _p.W = 1; // Use point weight, to mark that this is an inwards facing point
                }

                if (_p.Z % threshold == 0) // Only create intersection points if they fall within the user-defined threshold
                {
                    _p.X = InterpolateCoordinates(startVertex.X, endVertex.X, ratio);
                    _p.Y = InterpolateCoordinates(startVertex.Y, endVertex.Y, ratio);

                    intersections.Add(_p);
                }
            }
            return intersections;
        }

  
        private static double InterpolateCoordinates(double start, double end, double ratio)
        {
            return start + ratio * (end - start);
        }
        
    }
}
