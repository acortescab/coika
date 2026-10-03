using Coika.UI;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the anchor math of the <see cref="SafeAreaFitter"/> (issue #10): a notch inset becomes anchors, and a
    /// bad value from the platform gives the full screen instead of a collapsed HUD.
    /// </summary>
    public class SafeAreaFitterTests
    {
        private const float TOLERANCE = 0.0001f;

        /// <summary>
        /// A safe area equal to the screen anchors the full screen.
        /// </summary>
        [Test]
        public void ComputeAnchors_FullScreenSafeArea_ReturnsFullAnchors()
        {
            SafeAreaFitter.ComputeAnchors(new Rect(0f, 0f, 1080f, 2400f), 1080, 2400, out var min, out var max);

            Assert.That(min, Is.EqualTo(Vector2.zero));
            Assert.That(max, Is.EqualTo(Vector2.one));
        }

        /// <summary>
        /// A notch at the top and a bar at the bottom inset the anchors in proportion.
        /// </summary>
        [Test]
        public void ComputeAnchors_WithInsets_ReturnsProportionalAnchors()
        {
            SafeAreaFitter.ComputeAnchors(new Rect(0f, 120f, 1080f, 2160f), 1080, 2400, out var min, out var max);

            Assert.That(min.x, Is.EqualTo(0f).Within(TOLERANCE));
            Assert.That(min.y, Is.EqualTo(0.05f).Within(TOLERANCE));
            Assert.That(max.x, Is.EqualTo(1f).Within(TOLERANCE));
            Assert.That(max.y, Is.EqualTo(0.95f).Within(TOLERANCE));
        }

        /// <summary>
        /// A screen with no pixels or an empty safe area gives the full screen.
        /// </summary>
        [TestCase(0, 0, 0f, 0f, 0f, 0f)]
        [TestCase(1080, 1920, 0f, 0f, 0f, 0f)]
        [TestCase(0, 1920, 0f, 0f, 1080f, 1920f)]
        public void ComputeAnchors_WithInvalidValues_ReturnsFullAnchors(int width, int height, float x, float y, float areaWidth, float areaHeight)
        {
            SafeAreaFitter.ComputeAnchors(new Rect(x, y, areaWidth, areaHeight), width, height, out var min, out var max);

            Assert.That(min, Is.EqualTo(Vector2.zero));
            Assert.That(max, Is.EqualTo(Vector2.one));
        }

        /// <summary>
        /// An area that sticks out of the screen is clamped to the screen.
        /// </summary>
        [Test]
        public void ComputeAnchors_AreaOutsideTheScreen_ClampsToTheScreen()
        {
            SafeAreaFitter.ComputeAnchors(new Rect(-50f, -50f, 1200f, 2500f), 1080, 2400, out var min, out var max);

            Assert.That(min, Is.EqualTo(Vector2.zero));
            Assert.That(max, Is.EqualTo(Vector2.one));
        }
    }
}
