using System.Drawing;
using Microsoft.Azure.Kinect.Sensor;


namespace SandWorm.Analytics
{
    public class RGB : Analysis.MeshColorAnalysis
    {
        public RGB() : base("RGB")
        {
        }
        public Color[] GetColorCloudForAnalysis(Color[] pixelColors)
        {
            Color[] vertexColors = new Color[pixelColors.Length];
            for (int i = 0; i < pixelColors.Length; i++)
            {
                vertexColors[i] = Color.FromArgb(pixelColors[i].R, pixelColors[i].G, pixelColors[i].B);
            }
            
            return vertexColors; 
        }

        public override void ComputeLookupTableForAnalysis(double sensorElevation, double gradientRange)
        {
            return; // No lookup table necessary
        }
    }
}