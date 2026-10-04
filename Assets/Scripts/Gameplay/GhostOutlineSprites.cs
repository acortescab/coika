using System;
using System.Collections.Generic;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The dotted circle that outlines the landing ghost (GDD §6): one sprite per tier, as wide as the piece, one
    /// pixel thick, with nothing inside, so the ghost never hides what is under it. They are drawn once, in the load
    /// phase, so asking for one while playing allocates nothing (S-50). Like the dotted line, the sprites are
    /// point-sampled at 16 pixels per unit and stay crisp. The owner disposes it to free the textures.
    /// </summary>
    public sealed class GhostOutlineSprites : IDisposable
    {
        // Pixels on and off along the circle, as in the dot sprite of the line.
        private const int DASH_PIXELS = 2;

        private readonly Dictionary<TierDefinition, Sprite> _sprites = new();

        /// <summary>
        /// Draws the outline of every tier.
        /// </summary>
        /// <param name="tiers">The tiers whose pieces can be held.</param>
        /// <exception cref="ArgumentNullException">The tiers are null.</exception>
        public void Build(IReadOnlyList<TierDefinition> tiers)
        {
            if (tiers == null)
            {
                throw new ArgumentNullException(nameof(tiers));
            }

            foreach (var tier in tiers)
            {
                if (tier != null)
                {
                    Get(tier);
                }
            }
        }

        /// <summary>
        /// Returns the outline of a tier, drawing it first if it was not built (not expected while playing).
        /// </summary>
        /// <param name="tier">The tier of the held piece.</param>
        /// <returns>The dotted circle sprite of that tier.</returns>
        public Sprite Get(TierDefinition tier)
        {
            if (!_sprites.TryGetValue(tier, out var sprite))
            {
                sprite = CreateSprite(tier);
                _sprites.Add(tier, sprite);
            }

            return sprite;
        }

        /// <summary>
        /// Destroys the textures and sprites.
        /// </summary>
        public void Dispose()
        {
            foreach (var sprite in _sprites.Values)
            {
                if (sprite != null)
                {
                    UnityEngine.Object.Destroy(sprite.texture);
                    UnityEngine.Object.Destroy(sprite);
                }
            }

            _sprites.Clear();
        }

        /// <summary>
        /// Tells which pixels of a square of the given width belong to the dotted circle that touches its four
        /// sides. The circle is one pixel thick; going round it, two pixels are on and two are off. Pure, so it can
        /// be tested without textures.
        /// </summary>
        /// <param name="diameter">Width and height in pixels.</param>
        /// <returns>One flag per pixel, row by row from the bottom, true where the outline is drawn.</returns>
        public static bool[] CreateOutline(int diameter)
        {
            var size = Mathf.Max(1, diameter);
            var pixels = new bool[size * size];
            var centre = (size - 1) * 0.5f;
            var radius = size * 0.5f - 0.5f;
            var ring = new List<(float angle, int index)>();

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    if (distance >= radius - 0.5f && distance < radius + 0.5f)
                    {
                        ring.Add((Mathf.Atan2(y - centre, x - centre), y * size + x));
                    }
                }
            }

            ring.Sort((a, b) => a.angle.CompareTo(b.angle));
            for (var i = 0; i < ring.Count; i++)
            {
                pixels[ring[i].index] = (i / DASH_PIXELS) % 2 == 0;
            }

            return pixels;
        }

        /// <summary>
        /// Draws the sprite of a tier from <see cref="CreateOutline"/>, transparent everywhere else.
        /// </summary>
        private static Sprite CreateSprite(TierDefinition tier)
        {
            var size = Mathf.Max(1, Mathf.RoundToInt(tier.DiameterUnits * TierDefinition.PixelsPerUnit));
            var outline = CreateOutline(size);
            var colors = new Color32[outline.Length];
            for (var i = 0; i < outline.Length; i++)
            {
                colors[i] = outline[i] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "GhostOutline",
            };
            texture.SetPixels32(colors);
            texture.Apply(false, true);

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), TierDefinition.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
