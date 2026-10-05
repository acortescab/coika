using System;
using Coika.Fx;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the maths of the screen flash (<see cref="FlashCore"/>): the fade and the 3 Hz limit, on a fake clock.
    /// </summary>
    public class FlashCoreTests
    {
        private const float DURATION = 0.15f;
        private const float PEAK = 0.9f;

        /// <summary>
        /// A flash starts at the peak, fades linearly and is gone after its duration.
        /// </summary>
        [Test]
        public void Alpha_DuringAFlash_FadesFromPeakToZero()
        {
            var core = new FlashCore(3f, PEAK);

            Assert.IsTrue(core.TryFlash(DURATION, 10.0));

            Assert.AreEqual(PEAK, core.Alpha(10.0), 0.0001f);
            Assert.AreEqual(PEAK * 0.5f, core.Alpha(10.0 + DURATION * 0.5), 0.0001f);
            Assert.AreEqual(0f, core.Alpha(10.0 + DURATION));
        }

        /// <summary>
        /// Before any flash the overlay is clear.
        /// </summary>
        [Test]
        public void Alpha_WithoutFlash_IsZero()
        {
            Assert.AreEqual(0f, new FlashCore(3f, PEAK).Alpha(1.0));
        }

        /// <summary>
        /// A request sooner than a third of a second after the last accepted one is ignored.
        /// </summary>
        [Test]
        public void TryFlash_TooSoon_IsIgnored()
        {
            var core = new FlashCore(3f, PEAK);
            core.TryFlash(DURATION, 0.0);

            Assert.IsFalse(core.TryFlash(DURATION, 0.2));
            Assert.IsTrue(core.TryFlash(DURATION, 0.34));
        }

        /// <summary>
        /// A burst of ten requests in one second starts at most three flashes.
        /// </summary>
        [Test]
        public void TryFlash_BurstOfTenInOneSecond_StartsAtMostThree()
        {
            var core = new FlashCore(3f, PEAK);
            var accepted = 0;

            for (var i = 0; i < 10; i++)
            {
                if (core.TryFlash(DURATION, i * 0.1))
                {
                    accepted++;
                }
            }

            Assert.LessOrEqual(accepted, 3);
            Assert.GreaterOrEqual(accepted, 1);
        }

        /// <summary>
        /// Clearing hides the flash and accepts the next request at once.
        /// </summary>
        [Test]
        public void Clear_DuringAFlash_HidesItAndAcceptsTheNext()
        {
            var core = new FlashCore(3f, PEAK);
            core.TryFlash(DURATION, 0.0);

            core.Clear();

            Assert.AreEqual(0f, core.Alpha(0.01));
            Assert.IsTrue(core.TryFlash(DURATION, 0.02));
        }

        /// <summary>
        /// The constructor rejects a rate that is not above zero.
        /// </summary>
        [Test]
        public void Constructor_WithZeroRate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlashCore(0f, PEAK));
        }
    }
}
