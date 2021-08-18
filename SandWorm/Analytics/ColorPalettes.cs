using System.Collections.Generic;
using System.Drawing;

namespace SandWorm
{
    public static class ColorPalettes
    {
        public static Color[] GenerateColorPalettes(Structs.ColorPalettes palette, List<Color> customColors)
        {
            Color[] colorPalettes = new Color[5];

            switch (palette)
            {
                case Structs.ColorPalettes.Custom:
                    if (customColors.Count == 0)
                        break;
                    for (int i = 0; i < colorPalettes.Length; i++)
                        colorPalettes[i] = customColors[i];
                    break;

                case Structs.ColorPalettes.Chile:
                    colorPalettes[0] = Color.FromArgb(38, 115, 0);
                    colorPalettes[1] = Color.FromArgb(124, 191, 48);
                    colorPalettes[2] = Color.FromArgb(255, 247, 52);
                    colorPalettes[3] = Color.FromArgb(196, 65, 0);
                    colorPalettes[4] = Color.FromArgb(230, 188, 167);
                    break;

                case Structs.ColorPalettes.Desert:
                    colorPalettes[0] = Color.FromArgb(55, 101, 84);
                    colorPalettes[1] = Color.FromArgb(73, 117, 100);
                    colorPalettes[2] = Color.FromArgb(172, 196, 160);
                    colorPalettes[3] = Color.FromArgb(148, 131, 85);
                    colorPalettes[4] = Color.FromArgb(217, 209, 190);
                    break;

                case Structs.ColorPalettes.Europe:
                    colorPalettes[0] = Color.FromArgb(36, 121, 36);
                    colorPalettes[1] = Color.FromArgb(89, 148, 54);
                    colorPalettes[2] = Color.FromArgb(181, 195, 80);
                    colorPalettes[3] = Color.FromArgb(208, 191, 94);
                    colorPalettes[4] = Color.FromArgb(115, 24, 19);
                    break;

                case Structs.ColorPalettes.Greyscale:
                    colorPalettes[0] = Color.FromArgb(40, 40, 40);
                    colorPalettes[1] = Color.FromArgb(80, 80, 80);
                    colorPalettes[2] = Color.FromArgb(120, 120, 120);
                    colorPalettes[3] = Color.FromArgb(160, 160, 160);
                    colorPalettes[4] = Color.FromArgb(200, 200, 200);
                    break;

                case Structs.ColorPalettes.Dune:
                    colorPalettes[0] = Color.FromArgb(80, 80, 80);
                    colorPalettes[1] = Color.FromArgb(122, 91, 76);
                    colorPalettes[2] = Color.FromArgb(191, 118, 40);
                    colorPalettes[3] = Color.FromArgb(240, 173, 50);
                    colorPalettes[4] = Color.FromArgb(255, 210, 128);
                    break;

                case Structs.ColorPalettes.Ocean:
                    colorPalettes[0] = Color.FromArgb(47, 34, 58);
                    colorPalettes[1] = Color.FromArgb(62, 90, 146);
                    colorPalettes[2] = Color.FromArgb(80, 162, 162);
                    colorPalettes[3] = Color.FromArgb(152, 218, 164);
                    colorPalettes[4] = Color.FromArgb(250, 250, 200);
                    break;

                case Structs.ColorPalettes.Rainbow:
                    colorPalettes[0] = Color.FromArgb(0, 0, 255);
                    colorPalettes[1] = Color.FromArgb(0, 220, 255);
                    colorPalettes[2] = Color.FromArgb(140, 255, 110);
                    colorPalettes[3] = Color.FromArgb(255, 145, 0);
                    colorPalettes[4] = Color.FromArgb(255, 0, 0);
                    break;

                case Structs.ColorPalettes.Turbo:
                    colorPalettes[0] = Color.FromArgb(48, 18, 59);
                    colorPalettes[1] = Color.FromArgb(65, 69, 171);
                    colorPalettes[2] = Color.FromArgb(70,117,237);
                    colorPalettes[3] = Color.FromArgb(57, 162, 252);
                    colorPalettes[4] = Color.FromArgb(27, 207, 212);
                    colorPalettes[5] = Color.FromArgb(36, 236, 166);
                    colorPalettes[6] = Color.FromArgb(97, 252, 108);
                    colorPalettes[7] = Color.FromArgb(164, 252, 59);
                    colorPalettes[8] = Color.FromArgb(209, 232, 52);
                    colorPalettes[9] = Color.FromArgb(243, 198, 58);
                    colorPalettes[10] = Color.FromArgb(254, 155, 45);
                    colorPalettes[11] = Color.FromArgb(243, 99, 21);
                    colorPalettes[12] = Color.FromArgb(217, 56, 6);
                    colorPalettes[13] = Color.FromArgb(177, 25, 1);
                    colorPalettes[14] = Color.FromArgb(122, 4, 2);
                    break;
            }

            return colorPalettes;
        }
    }
}
