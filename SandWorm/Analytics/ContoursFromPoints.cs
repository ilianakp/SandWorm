using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Rhino.Geometry;


namespace SandWorm.Analytics
{
    public static class ContoursFromPoints
    {
        public static void GetGeometryForAnalysis(ref List<Line> contourLines, Point3d[] points, int threshold, 
            int trimmedWidth, int trimmedHeight, int ContourRoughness, double unitsMultiplier)
        {

            int xd = trimmedWidth;
            int yd = trimmedHeight;

            var ydd = (int)(yd / ContourRoughness);
            var xdd = (int)(xd / ContourRoughness);

            ConcurrentBag<Line> _contourLines = new ConcurrentBag<Line>();

            Parallel.For(1, ydd, y =>       // Iterate over y dimension
            {
                for (int x = 1; x < xdd; x++)       // Iterate over x dimension
                {
                    List<Point4d> intersectionPoints = new List<Point4d>();

                    int i = y * ContourRoughness * xd + (x * ContourRoughness);
                    int j = y * ContourRoughness * xd - (ContourRoughness * xd) + (x * ContourRoughness);

                    // lower left corner -> j - multiplier
                    // lower right corner -> j
                    // upper right corner-> i
                    // upper left corner -> i - multiplier

                    intersectionPoints.AddRange(FindIntersections(points[j - ContourRoughness], points[j], threshold, unitsMultiplier));
                    intersectionPoints.AddRange(FindIntersections(points[j], points[i], threshold, unitsMultiplier));
                    intersectionPoints.AddRange(FindIntersections(points[i], points[i - ContourRoughness], threshold, unitsMultiplier));
                    intersectionPoints.AddRange(FindIntersections(points[i - ContourRoughness], points[j - ContourRoughness], threshold, unitsMultiplier));

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


        private static List<Point4d> FindIntersections(Point3d startVertex, Point3d endVertex, int threshold, double unitsMultiplier)
        {
            List<Point4d> intersections = new List<Point4d>();
            Point4d _p = new Point4d();
            Point3d _startVertex = new Point3d(startVertex.X, startVertex.Y, startVertex.Z / unitsMultiplier);
            Point3d _endVertex = new Point3d(endVertex.X, endVertex.Y, endVertex.Z / unitsMultiplier);
            double deltaZ = Math.Abs(_endVertex.Z - _startVertex.Z);

            // Discard points if they have (0,0) coordinates (coming from the WFOV)
            if ((_startVertex.X == 0 && _startVertex.Y == 0) || (_endVertex.X == 0 && _endVertex.Y == 0))
                return intersections;

            // Discard points if they don't cross an isocurve 
            if ((int)_startVertex.Z == (int)_endVertex.Z || deltaZ == 0) 
                return intersections;

            double ratio = 0.0;

            for (int a = 1; a <= Math.Ceiling(deltaZ); a++)
            {
                if (_startVertex.Z < _endVertex.Z)
                {
                    _p.Z = Math.Floor(_startVertex.Z) + a;
                    ratio = (_p.Z - _startVertex.Z) / deltaZ;
                }
                else
                {
                    _p.Z = Math.Ceiling(_startVertex.Z) - a;
                    ratio = 1 - ((_p.Z - _endVertex.Z) / deltaZ);
                    _p.W = 1; // Use point weight, to mark that this is an inwards facing point
                }

                if (_p.Z % threshold == 0) // Only create intersection points if they fall within the user-defined threshold
                {
                    _p.X = InterpolateCoordinates(_startVertex.X, _endVertex.X, ratio);
                    _p.Y = InterpolateCoordinates(_startVertex.Y, _endVertex.Y, ratio);
                    _p.Z *= unitsMultiplier;

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
