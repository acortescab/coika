using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the Danger Line that <see cref="JarBuilder"/> creates and its show and pulse behaviour (issue #3, scope 3).
    /// </summary>
    public class DangerLineTests
    {
        private const float TOLERANCE = 0.0001f;

        private GameObject _jarObject;
        private Jar _jar;
        private GameConfig _config;

        /// <summary>
        /// Builds a jar with the GDD values (10 x 12.5, Drop Line 1.5 above the Danger Line).
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, null);
            _jarObject = new GameObject("Jar");
            _jar = _jarObject.AddComponent<Jar>();
            JarBuilder.Build(_jar, _config);
        }

        /// <summary>
        /// Destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_jarObject);
            Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// The line exists, sits at the Danger Line height, is as wide as the interior and is hidden by default.
        /// </summary>
        [Test]
        public void Build_Always_CreatesHiddenDangerLineAtDangerLineHeightWithInteriorWidth()
        {
            var line = _jar.DangerLine;
            var spriteRenderer = line.GetComponent<SpriteRenderer>();

            Assert.IsNotNull(line);
            Assert.AreEqual(_jar.DangerLineY, line.transform.position.y, TOLERANCE, "Height");
            Assert.AreEqual(SpriteDrawMode.Tiled, spriteRenderer.drawMode);
            Assert.AreEqual(10f, spriteRenderer.size.x, TOLERANCE, "Width");
            Assert.AreEqual(1f / 16f, spriteRenderer.size.y, TOLERANCE, "Thickness is one reference pixel");
            Assert.IsFalse(line.IsVisible, "The line must be hidden by default.");
        }

        /// <summary>
        /// Building again with another jar size resizes and moves the same line, keeps its state and adds no copy.
        /// </summary>
        [Test]
        public void Build_Again_ResizesTheSameLineAndKeepsItsState()
        {
            var line = _jar.DangerLine;
            line.SetVisible(true);
            line.SetPulse(true);
            var smallConfig = TestGameConfig.Create(new Vector2(8f, 10f), 1.5f, null);

            try
            {
                JarBuilder.Build(_jar, smallConfig);
            }
            finally
            {
                Object.DestroyImmediate(smallConfig);
            }

            Assert.AreSame(line, _jar.DangerLine);
            Assert.AreEqual(1, _jarObject.GetComponentsInChildren<DangerLine>().Length);
            Assert.AreEqual(8f, line.GetComponent<SpriteRenderer>().size.x, TOLERANCE, "Width");
            Assert.AreEqual(_jar.DangerLineY, line.transform.position.y, TOLERANCE, "Height");
            Assert.IsTrue(line.IsVisible);
            Assert.IsTrue(line.IsPulsing);
        }

        /// <summary>
        /// SetVisible shows and hides the line.
        /// </summary>
        [Test]
        public void SetVisible_WhenToggled_ShowsAndHidesTheLine()
        {
            var line = _jar.DangerLine;

            line.SetVisible(true);
            Assert.IsTrue(line.IsVisible);

            line.SetVisible(false);
            Assert.IsFalse(line.IsVisible);
        }

        /// <summary>
        /// The per-frame update is only enabled while the line is visible and pulsing, so an idle line costs nothing.
        /// </summary>
        [Test]
        public void SetPulse_WhenToggled_EnablesUpdateOnlyWhileVisibleAndPulsing()
        {
            var line = _jar.DangerLine;
            Assert.IsFalse(line.enabled, "Hidden and not pulsing.");

            line.SetPulse(true);
            Assert.IsFalse(line.enabled, "Pulsing but hidden.");

            line.SetVisible(true);
            Assert.IsTrue(line.enabled, "Visible and pulsing.");

            line.SetPulse(false);
            Assert.IsFalse(line.enabled, "Visible but not pulsing.");

            line.SetPulse(true);
            line.SetVisible(false);
            Assert.IsFalse(line.enabled, "Pulsing but hidden again.");
        }

        /// <summary>
        /// Stopping the pulse puts the line back to its idle colour.
        /// </summary>
        [Test]
        public void SetPulse_WhenStopped_RestoresIdleColor()
        {
            var line = _jar.DangerLine;
            var spriteRenderer = line.GetComponent<SpriteRenderer>();
            spriteRenderer.color = line.PulseColor;

            line.SetPulse(false);

            Assert.AreEqual(line.IdleColor, spriteRenderer.color);
        }

        /// <summary>
        /// The fast pulse reaches red at the peak of a 4 Hz wave and goes back to idle at the trough.
        /// </summary>
        [Test]
        public void EvaluateColor_WithFastRate_PeaksAndTroughsAt4Hz()
        {
            var line = _jar.DangerLine;

            Assert.AreEqual(4f, line.PulseHz, TOLERANCE);
            AssertColorEqual(line.PulseColor, line.EvaluateColor(1f / 16f), "Peak at 1/16 s");
            AssertColorEqual(line.IdleColor, line.EvaluateColor(3f / 16f), "Trough at 3/16 s");
        }

        /// <summary>
        /// The soft pulse is half as fast: it peaks at 1/8 s and troughs at 3/8 s.
        /// </summary>
        [Test]
        public void EvaluateColor_WithSoftRate_PeaksAndTroughsAt2Hz()
        {
            var line = _jar.DangerLine;
            line.SetPulseRate(DangerLinePulseRate.Soft);

            Assert.AreEqual(2f, line.PulseHz, TOLERANCE);
            AssertColorEqual(line.PulseColor, line.EvaluateColor(1f / 8f), "Peak at 1/8 s");
            AssertColorEqual(line.IdleColor, line.EvaluateColor(3f / 8f), "Trough at 3/8 s");
        }

        /// <summary>
        /// Compares two colours channel by channel with a small tolerance.
        /// </summary>
        /// <param name="expected">The expected colour.</param>
        /// <param name="actual">The colour to check.</param>
        /// <param name="message">Description shown when they differ.</param>
        private static void AssertColorEqual(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f, message + " (r)");
            Assert.AreEqual(expected.g, actual.g, 0.001f, message + " (g)");
            Assert.AreEqual(expected.b, actual.b, 0.001f, message + " (b)");
            Assert.AreEqual(expected.a, actual.a, 0.001f, message + " (a)");
        }
    }
}
