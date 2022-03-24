using System.Collections.Generic;
using Rhino.Geometry;

namespace SandWorm.Analytics
{
    public static class WaterLevel
    {
        public static void GetGeometryForAnalysis(ref List<GeometryBase> outputGeometry, double waterLevel, Mesh terrainMesh)
        {
            List<Point3d> waterCorners = new List<Point3d>();
            BoundingBox box = terrainMesh.GetBoundingBox(false);
            Point3d[] corners = box.GetCorners();

            waterCorners.Add(corners[0]);
            waterCorners.Add(corners[1]);
            waterCorners.Add(corners[2]);
            waterCorners.Add(corners[3]);
            waterCorners.Add(corners[0]); // Close polyline

            for (int i = 0; i < waterCorners.Count; i++)
                waterCorners[i] = new Point3d(waterCorners[i].X, waterCorners[i].Y, waterLevel * SandWormComponent.unitsMultiplier);

            Polyline waterBoundary = new Polyline(waterCorners);
            Mesh waterPlane = Mesh.CreateFromClosedPolyline(waterBoundary);

            //Setting mesh transparency doesn't work. It's a known bug https://mcneel.myjetbrains.com/youtrack/issue/RH-49604
            //waterPlane.VertexColors.CreateMonotoneMesh(System.Drawing.Color.FromArgb(100, 100, 100, 255));
            outputGeometry.Add(waterPlane);
        }
    }
}