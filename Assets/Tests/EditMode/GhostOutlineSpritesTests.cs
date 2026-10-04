using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests the pixels of the dotted circle that outlines the landing ghost (issue #28).
    /// </summary>
    public class GhostOutlineSpritesTests
    {
        /// <summary>
        /// Nothing is drawn inside the circle, so the ghost has no fill.
        /// </summary>
        [Test]
        public void CreateOutline_AnyDiameter_LeavesTheInsideEmpty([Values(6, 16, 31, 48)] int diameter)
        {
            var pixels = GhostOutlineSprites.CreateOutline(diameter);
            var centre = (diameter - 1) * 0.5f;
            var radius = diameter * 0.5f - 0.5f;

            for (var y = 0; y < diameter; y++)
            {
                for (var x = 0; x < diameter; x++)
                {
                    var distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    if (distance < radius - 1f)
                    {
                        Assert.IsFalse(pixels[y * diameter + x], $"pixel {x},{y} is inside the circle");
                    }
                }
            }
        }

        /// <summary>
        /// The circle is dotted: some pixels of the ring are drawn and some are not, about half each.
        /// </summary>
        [Test]
        public void CreateOutline_AnyDiameter_DrawsAboutHalfOfTheRing([Values(16, 31, 48)] int diameter)
        {
            var pixels = GhostOutlineSprites.CreateOutline(diameter);
            var centre = (diameter - 1) * 0.5f;
            var radius = diameter * 0.5f - 0.5f;
            var ring = 0;
            var on = 0;

            for (var y = 0; y < diameter; y++)
            {
                for (var x = 0; x < diameter; x++)
                {
                    var distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    if (distance >= radius - 0.5f && distance < radius + 0.5f)
                    {
                        ring++;
                        on += pixels[y * diameter + x] ? 1 : 0;
                    }
                }
            }

            Assert.Greater(on, 0, "Something is drawn.");
            Assert.AreEqual(ring * 0.5f, on, ring * 0.15f + 1f, "About half of the ring is on.");
        }
    }
}
