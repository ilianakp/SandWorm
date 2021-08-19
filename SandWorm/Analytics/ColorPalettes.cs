using System.Collections.Generic;
using System.Drawing;

namespace SandWorm
{
    public static class ColorPalettes
    {
        public static List<Color> GenerateColorPalettes(Structs.ColorPalettes palette, List<Color> customColors)
        {
            List<Color> paletteSwatches = new List<Color>();

            switch (palette)
            {
                case Structs.ColorPalettes.Custom:
                    if (customColors.Count == 0) // No inputs provided; use placeholder
                        paletteSwatches.Add(Color.FromArgb(122, 122, 122));
                        paletteSwatches.Add(Color.FromArgb(122, 122, 122));

                    for (int i = 0; i < customColors.Count; i++)
                        paletteSwatches.Add(customColors[i]);
                    break;

                case Structs.ColorPalettes.Chile:
                    paletteSwatches.Add(Color.FromArgb(38, 115, 0));
                    paletteSwatches.Add(Color.FromArgb(124, 191, 48));
                    paletteSwatches.Add(Color.FromArgb(255, 247, 52));
                    paletteSwatches.Add(Color.FromArgb(196, 65, 0));
                    paletteSwatches.Add(Color.FromArgb(230, 188, 167));
                    break;

                case Structs.ColorPalettes.Desert:
                    paletteSwatches.Add(Color.FromArgb(55, 101, 84));
                    paletteSwatches.Add(Color.FromArgb(73, 117, 100));
                    paletteSwatches.Add(Color.FromArgb(172, 196, 160));
                    paletteSwatches.Add(Color.FromArgb(148, 131, 85));
                    paletteSwatches.Add(Color.FromArgb(217, 209, 190));
                    break;

                case Structs.ColorPalettes.Europe:
                    paletteSwatches.Add(Color.FromArgb(36, 121, 36));
                    paletteSwatches.Add(Color.FromArgb(89, 148, 54));
                    paletteSwatches.Add(Color.FromArgb(181, 195, 80));
                    paletteSwatches.Add(Color.FromArgb(208, 191, 94));
                    paletteSwatches.Add(Color.FromArgb(115, 24, 19));
                    break;

                case Structs.ColorPalettes.Greyscale:
                    paletteSwatches.Add(Color.FromArgb(40, 40, 40));
                    paletteSwatches.Add(Color.FromArgb(80, 80, 80));
                    paletteSwatches.Add(Color.FromArgb(120, 120, 120));
                    paletteSwatches.Add(Color.FromArgb(160, 160, 160));
                    paletteSwatches.Add(Color.FromArgb(200, 200, 200));
                    break;

                case Structs.ColorPalettes.Dune:
                    paletteSwatches.Add(Color.FromArgb(80, 80, 80));
                    paletteSwatches.Add(Color.FromArgb(122, 91, 76));
                    paletteSwatches.Add(Color.FromArgb(191, 118, 40));
                    paletteSwatches.Add(Color.FromArgb(240, 173, 50));
                    paletteSwatches.Add(Color.FromArgb(255, 210, 128));
                    break;

                case Structs.ColorPalettes.Ocean:
                    paletteSwatches.Add(Color.FromArgb(47, 34, 58));
                    paletteSwatches.Add(Color.FromArgb(62, 90, 146));
                    paletteSwatches.Add(Color.FromArgb(80, 162, 162));
                    paletteSwatches.Add(Color.FromArgb(152, 218, 164));
                    paletteSwatches.Add(Color.FromArgb(250, 250, 200));
                    break;

                case Structs.ColorPalettes.Turbo:
                    paletteSwatches.Add(Color.FromArgb(48, 18, 59));
                    paletteSwatches.Add(Color.FromArgb(65, 69, 171));
                    paletteSwatches.Add(Color.FromArgb(70,117,237));
                    paletteSwatches.Add(Color.FromArgb(57, 162, 252));
                    paletteSwatches.Add(Color.FromArgb(27, 207, 212));
                    paletteSwatches.Add(Color.FromArgb(36, 236, 166));
                    paletteSwatches.Add(Color.FromArgb(97, 252, 108));
                    paletteSwatches.Add(Color.FromArgb(164, 252, 59));
                    paletteSwatches.Add(Color.FromArgb(209, 232, 52));
                    paletteSwatches.Add(Color.FromArgb(243, 198, 58));
                    paletteSwatches.Add(Color.FromArgb(254, 155, 45));
                    paletteSwatches.Add(Color.FromArgb(243, 99, 21));
                    paletteSwatches.Add(Color.FromArgb(217, 56, 6));
                    paletteSwatches.Add(Color.FromArgb(177, 25, 1));
                    paletteSwatches.Add(Color.FromArgb(122, 4, 2));
                    break;

                case Structs.ColorPalettes.Viridis:
                    paletteSwatches.Add(Color.FromArgb(52, 0, 66));
                    paletteSwatches.Add(Color.FromArgb(55, 8, 85));
                    paletteSwatches.Add(Color.FromArgb(55, 23, 100));
                    paletteSwatches.Add(Color.FromArgb(53, 37, 110));
                    paletteSwatches.Add(Color.FromArgb(48, 52, 117));
                    paletteSwatches.Add(Color.FromArgb(44, 65, 121));
                    paletteSwatches.Add(Color.FromArgb(39, 80, 123));
                    paletteSwatches.Add(Color.FromArgb(36, 93, 123));
                    paletteSwatches.Add(Color.FromArgb(33, 106, 123));
                    paletteSwatches.Add(Color.FromArgb(31, 120, 122));
                    paletteSwatches.Add(Color.FromArgb(30, 134, 120));
                    paletteSwatches.Add(Color.FromArgb(32, 148, 115));
                    paletteSwatches.Add(Color.FromArgb(38, 162, 108));
                    paletteSwatches.Add(Color.FromArgb(52, 178, 98));
                    paletteSwatches.Add(Color.FromArgb(73, 190, 84));
                    paletteSwatches.Add(Color.FromArgb(101, 202, 68));
                    paletteSwatches.Add(Color.FromArgb(132, 212, 50));
                    paletteSwatches.Add(Color.FromArgb(171, 219, 32));
                    paletteSwatches.Add(Color.FromArgb(212, 225, 21));
                    paletteSwatches.Add(Color.FromArgb(252, 229, 30));
                    break;
            }

            return paletteSwatches;
        }
    }
}
