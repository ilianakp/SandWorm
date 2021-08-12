using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Rhino.Geometry;


namespace SandWorm.Analytics
{
    public static class ContoursFromPoints
    {
        public static void GetGeometryForAnalysis(ref List<Line> contourLines, Point3d[] points, int threshold, int trimmedWidth, int trimmedHeight)
        {
            int xd = trimmedWidth;
            int yd = trimmedHeight;

            ConcurrentBag<Point4d> test = new ConcurrentBag<Point4d>();
            ConcurrentBag<Line> _contourLines = new ConcurrentBag<Line>();

            Parallel.For(1, yd - 1, y =>       // Iterate over y dimension
            {
                for (int x = 1; x < xd - 1; x++)       // Iterate over x dimension
                {
                    List<Point4d> intersectionPoints = new List<Point4d>();

                    int i = y * xd + x;
                    int j = (y - 1) * xd + x;

                    // lower left corner -> j - 1
                    // lower right corner -> j
                    // upper right corner-> i
                    // upper left corner -> i - 1

                    intersectionPoints.AddRange(FindIntersections(points[j - 1], points[j], threshold));
                    intersectionPoints.AddRange(FindIntersections(points[j], points[i], threshold));
                    intersectionPoints.AddRange(FindIntersections(points[i], points[i - 1], threshold));
                    intersectionPoints.AddRange(FindIntersections(points[i - 1], points[j - 1], threshold));

                    foreach (var p in intersectionPoints)
                        test.Add(p);

                    if (intersectionPoints.Count > 0)
                    {
                        Stack<Point4d> vertexStack = new Stack<Point4d>();

                        for (int a = 0; a < intersectionPoints.Count; a++)
                        {
                            if (vertexStack.Count == 0 || vertexStack.Peek().W == intersectionPoints[a].W)
                                vertexStack.Push(intersectionPoints[a]);
                            else
                            {
                                Point4d _start = vertexStack.Pop();
                                Point4d _end = intersectionPoints[a];
                                if (Math.Abs(_start.X - _end.X) < 30 && Math.Abs(_start.Y - _end.Y) < 30)
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

            for (int a = 0; a < deltaZ; a++)
            {
                if (startVertex.Z < endVertex.Z)
                    _p.Z = startVertex.Z + a + 1;
                else
                {
                    _p.Z = startVertex.Z - a - 1;
                    _p.W = 1; // Use point weight, to mark that this is an inwards facing point
                }

                if (Math.Round(_p.Z) % threshold == 0) // Only create intersection points if they fall within the user-defined threshold
                {
                    _p.X = startVertex.X + ((a + 1) * (endVertex.X - startVertex.X) / deltaZ);
                    _p.Y = startVertex.Y + ((a + 1) * (endVertex.Y - startVertex.Y) / deltaZ);

                    intersections.Add(_p);
                }
            }

            return intersections;
        }
    }
}
