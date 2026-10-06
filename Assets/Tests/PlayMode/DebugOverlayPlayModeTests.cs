#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Coika.UI;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the development overlay of issue #39: it reports the frame rate, the worst frame and the allocation
    /// count of a window, and sampling a frame allocates nothing.
    /// </summary>
    public class DebugOverlayPlayModeTests : HarnessTestBase
    {
        private const int SAMPLES = 1000;
        private const float FRAME_SECONDS = 0.02f;

        private GameObject _host;
        private DebugOverlay _overlay;

        /// <summary>
        /// Builds an overlay on an inactive host, so only the test drives it.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("TestOverlay");
            _host.SetActive(false);
            _overlay = _host.AddComponent<DebugOverlay>();
        }

        /// <summary>
        /// Destroys the host.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        /// <summary>
        /// After one window of frames of 20 ms the overlay shows 50 FPS, a 20 ms worst frame and the largest
        /// allocation count seen.
        /// </summary>
        [Test]
        public void Sample_AfterOneWindow_ReportsFpsWorstFrameAndAllocations()
        {
            // 26 frames, not 25: the sum of 25 floats of 0.02 can land just under the 0.5 s window.
            for (var frame = 0; frame < 26; frame++)
            {
                _overlay.Sample(FRAME_SECONDS, frame == 3 ? 7 : 0);
            }

            Assert.AreEqual(50, _overlay.Fps);
            Assert.AreEqual(20, _overlay.MaxFrameMs);
            Assert.AreEqual(7, _overlay.MaxGcAllocations);
        }

        /// <summary>
        /// A slow frame shows as the worst frame of the window.
        /// </summary>
        [Test]
        public void Sample_WithASpike_ReportsTheSpikeAsTheWorstFrame()
        {
            for (var frame = 0; frame < 24; frame++)
            {
                _overlay.Sample(FRAME_SECONDS, 0);
            }

            _overlay.Sample(0.05f, 0); // 0.48 s + 0.05 s closes the window

            Assert.AreEqual(50, _overlay.MaxFrameMs);
        }

        /// <summary>
        /// Sampling frames, including the end of every window, allocates nothing.
        /// </summary>
        [Test]
        public void Sample_ManyFrames_AllocatesNothing()
        {
            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < SAMPLES; i++)
                {
                    _overlay.Sample(FRAME_SECONDS, 0);
                }
            });

            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT);
        }
    }
}
#endif
