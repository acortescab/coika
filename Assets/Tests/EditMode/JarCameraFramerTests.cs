using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the camera framing maths of <see cref="JarCameraFramer"/> with the GDD values (issue #3, scope 4):
    /// a 192 x 320 px reference frame at PPU 16 is 12 x 20 units, the HUD margin is 40 px (2.5 units) and the
    /// jar interior is 10 x 12.5 units with the Drop Line 1.5 above the Danger Line.
    /// </summary>
    public class JarCameraFramerTests
    {
        private const float TOLERANCE = 0.0001f;
        private const float PPU = 16f;
        private const float REFERENCE_HEIGHT = 20f;
        private const float HUD_MARGIN = 2.5f;

        private static readonly Vector2 ReferenceSize = new(12f, 20f);

        /// <summary>
        /// With the GDD jar on the origin the camera is centred on the jar and 6.5 units up, so the frame spans
        /// from -3.5 to 16.5: the floor is fully visible and the top edge is the Drop Line plus the HUD margin.
        /// </summary>
        [Test]
        public void ComputeCenter_WithGddJar_CentersJarAndKeepsHudMarginAboveDropLine()
        {
            var center = JarCameraFramer.ComputeCenter(new Vector2(-5f, 0f), new Vector2(5f, 12.5f), 14f, REFERENCE_HEIGHT, HUD_MARGIN, PPU);

            Assert.AreEqual(0f, center.x, TOLERANCE, "Horizontally centred");
            Assert.AreEqual(6.5f, center.y, TOLERANCE, "Camera height");
            Assert.AreEqual(14f + HUD_MARGIN, center.y + REFERENCE_HEIGHT * 0.5f, TOLERANCE, "Frame top = Drop Line + HUD margin");
            Assert.AreEqual(-3.5f, center.y - REFERENCE_HEIGHT * 0.5f, TOLERANCE, "Frame bottom");
        }

        /// <summary>
        /// A jar that is not on the origin is followed: the camera centres on the jar and keeps the same offsets.
        /// </summary>
        [Test]
        public void ComputeCenter_WithOffsetJar_FollowsTheJar()
        {
            var center = JarCameraFramer.ComputeCenter(new Vector2(-3f, 4f), new Vector2(7f, 16.5f), 18f, REFERENCE_HEIGHT, HUD_MARGIN, PPU);

            Assert.AreEqual(2f, center.x, TOLERANCE, "Jar centre");
            Assert.AreEqual(10.5f, center.y, TOLERANCE, "Camera height");
        }

        /// <summary>
        /// The camera lands on a whole pixel (a multiple of 1/16 unit), so the Pixel Perfect Camera does not shimmer.
        /// </summary>
        [Test]
        public void ComputeCenter_WithValuesBetweenPixels_SnapsToWholePixels()
        {
            var center = JarCameraFramer.ComputeCenter(new Vector2(-4.97f, 0f), new Vector2(5.01f, 12.5f), 14.03f, REFERENCE_HEIGHT, HUD_MARGIN, PPU);

            Assert.AreEqual(0f, (center.x * PPU) % 1f, TOLERANCE, "x on the pixel grid");
            Assert.AreEqual(0f, (center.y * PPU) % 1f, TOLERANCE, "y on the pixel grid");
        }

        /// <summary>
        /// The GDD jar, 10 units of interior plus two 1-unit walls, fits the 12 unit frame width exactly.
        /// </summary>
        [Test]
        public void Fits_WithGddJar_ReturnsTrue()
        {
            Assert.IsTrue(JarCameraFramer.Fits(10f, 14f, ReferenceSize, HUD_MARGIN, 1f));
        }

        /// <summary>
        /// A jar whose interior plus its two walls is wider than the frame does not fit.
        /// </summary>
        [Test]
        public void Fits_WithJarWiderThanFrame_ReturnsFalse()
        {
            Assert.IsFalse(JarCameraFramer.Fits(11f, 14f, ReferenceSize, HUD_MARGIN, 1f));
        }

        /// <summary>
        /// A jar too tall for the frame, counting the floor and the HUD margin, does not fit.
        /// </summary>
        [Test]
        public void Fits_WithJarTallerThanFrame_ReturnsFalse()
        {
            Assert.IsFalse(JarCameraFramer.Fits(10f, 17f, ReferenceSize, HUD_MARGIN, 1f));
        }
    }
}
