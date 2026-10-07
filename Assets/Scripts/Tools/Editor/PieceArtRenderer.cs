using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Draws the pixels of the piece bodies and chart icons from <see cref="CoikaPalette"/>: a hard-edged circle that
    /// touches the canvas edges, a 1 px outline, a lower-right shade and a top-left highlight, with no anti-aliasing.
    /// The output depends only on its arguments, so it is repeatable.
    /// </summary>
    public static class PieceArtRenderer
    {
        private const float OUTLINE_WIDTH = 1f;
        private const float HIGHLIGHT_OFFSET = 0.38f;
        private const float HIGHLIGHT_RADIUS = 0.3f;
        private const float SHADE_THRESHOLD = -0.45f;
        private const float SQRT_TWO = 1.4142135f;

        /// <summary>
        /// Draws a tier body. Row 0 is the bottom row, as in <see cref="Texture2D.SetPixels32(Color32[])"/>.
        /// </summary>
        /// <param name="tierIndex">Tier index, 0 to 10, which selects the colours.</param>
        /// <param name="size">Side of the square canvas in pixels; equals the circle diameter.</param>
        /// <param name="simplified">True to draw only the outline, the fill and a highlight, without the shade.</param>
        /// <returns>The pixels, size x size, transparent outside the circle.</returns>
        public static Color32[] Render(int tierIndex, int size, bool simplified)
        {
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f;
            var highlightX = center - HIGHLIGHT_OFFSET * radius;
            var highlightY = center + HIGHLIGHT_OFFSET * radius;
            var highlightRadius = Mathf.Max(HIGHLIGHT_RADIUS * radius, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = PixelAt(tierIndex, simplified, distance, radius, dx, dy, x - highlightX, y - highlightY, highlightRadius);
                }
            }

            return pixels;
        }

        /// <summary>
        /// Chooses the colour of one pixel.
        /// </summary>
        /// <param name="tierIndex">Tier index selecting the colours.</param>
        /// <param name="simplified">True to skip the shade.</param>
        /// <param name="distance">Distance of the pixel from the canvas centre.</param>
        /// <param name="radius">Radius of the circle.</param>
        /// <param name="dx">Horizontal offset from the centre.</param>
        /// <param name="dy">Vertical offset from the centre, positive upwards.</param>
        /// <param name="highlightDx">Horizontal offset from the highlight centre.</param>
        /// <param name="highlightDy">Vertical offset from the highlight centre.</param>
        /// <param name="highlightRadius">Radius of the highlight spot.</param>
        /// <returns>A palette colour, or transparent outside the circle.</returns>
        private static Color32 PixelAt(int tierIndex, bool simplified, float distance, float radius, float dx, float dy, float highlightDx, float highlightDy, float highlightRadius)
        {
            if (distance > radius)
            {
                return new Color32(0, 0, 0, 0);
            }

            if (distance > radius - OUTLINE_WIDTH)
            {
                return CoikaPalette.Outline;
            }

            if (Mathf.Sqrt(highlightDx * highlightDx + highlightDy * highlightDy) <= highlightRadius)
            {
                return CoikaPalette.Highlight(tierIndex);
            }

            var light = (dy - dx) / (SQRT_TWO * radius);
            if (!simplified && light < SHADE_THRESHOLD)
            {
                return CoikaPalette.Shade(tierIndex);
            }

            return CoikaPalette.Base(tierIndex);
        }
    }
}
