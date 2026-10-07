using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// The one shared palette of the piece art (GDD §10): the shared dark outline, the 11 tier colours of GDD §9 and,
    /// per tier, a shade and a highlight derived from it. All piece bodies and chart icons draw only these colours,
    /// and the palette never exceeds <see cref="MAX_COLORS"/> distinct colours.
    /// </summary>
    public static class CoikaPalette
    {
        public const int MAX_COLORS = 32;
        public const string GplPath = "Assets/Art/Palette/coika.gpl";

        private const int TIER_COUNT = 11;
        private const int SHADE_PERCENT = 70;
        private const int HIGHLIGHT_PERCENT = 45;

        /// <summary>
        /// The shared dark outline of every body and icon; the black hole uses it as its shade as well.
        /// </summary>
        public static readonly Color32 Outline = new Color32(0x1B, 0x12, 0x30, 0xFF);

        private static readonly Color32 White = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        // Tier colours of GDD §9, in tier order.
        private static readonly Color32[] Bases =
        {
            Hex(0x8E8E8E), Hex(0xB39B7A), Hex(0x7A7A9E), Hex(0xC8D0DC), Hex(0xD9A66B), Hex(0xC4623D),
            Hex(0x3D86C4), Hex(0xD99B3D), Hex(0xFFD84A), Hex(0x9AF0FF), Hex(0x2B1A4D),
        };

        /// <summary>
        /// The tier colour of GDD §9.
        /// </summary>
        /// <param name="tierIndex">Tier index, 0 to 10.</param>
        /// <returns>The flat fill colour of the tier.</returns>
        public static Color32 Base(int tierIndex)
        {
            return Bases[tierIndex];
        }

        /// <summary>
        /// The darker colour of the lower-right crescent of a body. The black hole has no darker colour than its
        /// outline, so it uses the outline.
        /// </summary>
        /// <param name="tierIndex">Tier index, 0 to 10.</param>
        /// <returns>The shade colour of the tier.</returns>
        public static Color32 Shade(int tierIndex)
        {
            if (tierIndex == TIER_COUNT - 1)
            {
                return Outline;
            }

            return Scale(Bases[tierIndex], SHADE_PERCENT);
        }

        /// <summary>
        /// The lighter colour of the top-left highlight. The two already-pale tiers (Moon and Neutron Star) share white.
        /// </summary>
        /// <param name="tierIndex">Tier index, 0 to 10.</param>
        /// <returns>The highlight colour of the tier.</returns>
        public static Color32 Highlight(int tierIndex)
        {
            if (tierIndex == 3 || tierIndex == 9)
            {
                return White;
            }

            return Lighten(Bases[tierIndex], HIGHLIGHT_PERCENT);
        }

        /// <summary>
        /// Lists every distinct colour of the palette in a stable order.
        /// </summary>
        /// <returns>The outline, then per tier the base, the shade and the highlight, without duplicates.</returns>
        public static IReadOnlyList<Color32> Colors()
        {
            var colors = new List<Color32> { Outline };
            for (int tier = 0; tier < TIER_COUNT; tier++)
            {
                colors.Add(Bases[tier]);
                colors.Add(Shade(tier));
                colors.Add(Highlight(tier));
            }

            return colors.Distinct().ToList();
        }

        /// <summary>
        /// Renders the palette in the GIMP palette format.
        /// </summary>
        /// <returns>The text of the .gpl file, with LF line endings.</returns>
        public static string ToGpl()
        {
            var builder = new StringBuilder();
            builder.Append("GIMP Palette\nName: Coika\nColumns: 8\n#\n");
            foreach (var color in Colors())
            {
                builder.Append($"{color.r,3} {color.g,3} {color.b,3}\t{ToHex(color)}\n");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Writes <see cref="ToGpl"/> to <see cref="GplPath"/> when the file is missing or different.
        /// </summary>
        /// <returns>True when the file was written.</returns>
        public static bool WriteGpl()
        {
            var text = ToGpl();
            if (File.Exists(GplPath) && File.ReadAllText(GplPath) == text)
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(GplPath));
            File.WriteAllText(GplPath, text);
            return true;
        }

        /// <summary>
        /// Formats a colour as an upper-case hex code.
        /// </summary>
        /// <param name="color">The colour.</param>
        /// <returns>A string like #8E8E8E.</returns>
        public static string ToHex(Color32 color)
        {
            return $"#{color.r:X2}{color.g:X2}{color.b:X2}";
        }

        /// <summary>
        /// Builds an opaque colour from a 24-bit RGB value.
        /// </summary>
        /// <param name="rgb">The value 0xRRGGBB.</param>
        /// <returns>The opaque colour.</returns>
        private static Color32 Hex(int rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);
        }

        /// <summary>
        /// Multiplies each channel by a percentage, using integer maths so the result is exact and repeatable.
        /// </summary>
        /// <param name="color">The colour to darken.</param>
        /// <param name="percent">Percentage of the original channel values.</param>
        /// <returns>The darker colour.</returns>
        private static Color32 Scale(Color32 color, int percent)
        {
            return new Color32((byte)(color.r * percent / 100), (byte)(color.g * percent / 100), (byte)(color.b * percent / 100), 0xFF);
        }

        /// <summary>
        /// Moves each channel towards 255 by a percentage of the remaining distance, using integer maths.
        /// </summary>
        /// <param name="color">The colour to lighten.</param>
        /// <param name="percent">Percentage of the distance to white.</param>
        /// <returns>The lighter colour.</returns>
        private static Color32 Lighten(Color32 color, int percent)
        {
            return new Color32(
                (byte)(color.r + (255 - color.r) * percent / 100),
                (byte)(color.g + (255 - color.g) * percent / 100),
                (byte)(color.b + (255 - color.b) * percent / 100),
                0xFF);
        }
    }
}
