using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace SandWorm
{
    public class MyComponent1 : GH_Component
    {

        public MyComponent1()
          : base("Sandworm Contour Test", "Test",
            "Test", "SandWorm", "Visualisation")
        {
        }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("points", "points", "", GH_ParamAccess.list);
            pManager.AddIntegerParameter("threshhold", "threshhold", "", GH_ParamAccess.item);
            pManager.AddIntegerParameter("interval", "interval", "", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("out", "out", "", GH_ParamAccess.list);
            pManager.AddCurveParameter("curves", "curve", "", GH_ParamAccess.list);
        }


        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Point3d> outpoints = new List<Point3d>();
            List<Point3d> pts = new List<Point3d>();
            Point3d[] points = new Point3d[51 * 51];
            List<Line> lines = new List<Line>();

            int threshhold = 1;
            int interval = 1;

            DA.GetDataList(0, pts);
            DA.GetData(1, ref threshhold);
            DA.GetData(2, ref interval);

            for (int i = 0; i < pts.Count; i++)
                points[i] = pts[i];

            SandWorm.Analytics.ContoursFromPoints.GetGeometryForAnalysis(ref lines, points, threshhold, 4, 2, interval);

            //DA.SetDataList(0, outpoints);
            DA.SetDataList(1, lines);
        }


        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }


        public override Guid ComponentGuid
        {
            get { return new Guid("ca780e22-a86d-4aed-a53e-9590f27de87a"); }
        }
    }
}