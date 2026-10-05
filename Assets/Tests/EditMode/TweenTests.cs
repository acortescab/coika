using System;
using Coika.Core.Animation;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the easing maths of <see cref="Tween"/> (issue #31) with exact values.
    /// </summary>
    public class TweenTests
    {
        private const float TOLERANCE = 0.0001f;

        /// <summary>
        /// Progress is the elapsed time over the duration, clamped to 0 to 1.
        /// </summary>
        [TestCase(0f, 0.2f, 0f)]
        [TestCase(0.1f, 0.2f, 0.5f)]
        [TestCase(0.2f, 0.2f, 1f)]
        [TestCase(5f, 0.2f, 1f)]
        [TestCase(-1f, 0.2f, 0f)]
        public void Progress_WithADuration_IsClampedElapsedOverDuration(float elapsed, float duration, float expected)
        {
            Assert.AreEqual(expected, Tween.Progress(elapsed, duration), TOLERANCE);
        }

        /// <summary>
        /// A zero or negative duration counts as finished.
        /// </summary>
        [Test]
        public void Progress_WithNoDuration_IsOne()
        {
            Assert.AreEqual(1f, Tween.Progress(0f, 0f));
            Assert.AreEqual(1f, Tween.Progress(0f, -1f));
        }

        /// <summary>
        /// The back ease starts at 0, goes past 1 and ends at exactly 1.
        /// </summary>
        [Test]
        public void OutBack_OverTheDuration_StartsAtZeroOvershootsAndEndsAtOne()
        {
            var max = 0f;
            for (var i = 0; i <= 100; i++)
            {
                max = Math.Max(max, Tween.OutBack(i / 100f, Tween.DEFAULT_OVERSHOOT));
            }

            Assert.AreEqual(0f, Tween.OutBack(0f, Tween.DEFAULT_OVERSHOOT), TOLERANCE);
            Assert.AreEqual(1f, Tween.OutBack(1f, Tween.DEFAULT_OVERSHOOT), TOLERANCE);
            Assert.Greater(max, 1f);
        }

        /// <summary>
        /// Without overshoot the back ease never goes past 1.
        /// </summary>
        [Test]
        public void OutBack_WithNoOvershoot_NeverExceedsOne()
        {
            for (var i = 0; i <= 100; i++)
            {
                Assert.LessOrEqual(Tween.OutBack(i / 100f, 0f), 1f + TOLERANCE);
            }
        }

        /// <summary>
        /// The pop starts at 0, reaches the peak at the peak time and ends at 1.
        /// </summary>
        [Test]
        public void PopThrough_OverTheDuration_HitsTheStartThePeakAndTheEnd()
        {
            Assert.AreEqual(0f, Tween.PopThrough(0f, 1.2f, 0.5f), TOLERANCE);
            Assert.AreEqual(1.2f, Tween.PopThrough(0.5f, 1.2f, 0.5f), TOLERANCE);
            Assert.AreEqual(1f, Tween.PopThrough(1f, 1.2f, 0.5f), TOLERANCE);
        }

        /// <summary>
        /// A peak at or outside the ends of the animation is a programming error.
        /// </summary>
        [TestCase(0f)]
        [TestCase(1f)]
        public void PopThrough_WithThePeakAtAnEnd_Throws(float peakAt)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Tween.PopThrough(0.5f, 1.2f, peakAt));
        }

        /// <summary>
        /// A timer is active from its start until the step that reaches its duration, and stops there.
        /// </summary>
        [Test]
        public void TweenTimer_OverItsDuration_IsActiveUntilTheDurationIsReached()
        {
            var timer = new TweenTimer();
            Assert.IsFalse(timer.IsActive, "A new timer is not active.");

            timer.Start(0.1f);
            timer.Advance(0.05f);
            Assert.IsTrue(timer.IsActive);
            Assert.AreEqual(0.5f, timer.Progress, TOLERANCE);

            timer.Advance(0.05f);
            Assert.IsFalse(timer.IsActive, "It ends on the step that reaches the duration.");
            Assert.AreEqual(1f, timer.Progress, TOLERANCE);
        }

        /// <summary>
        /// Stopping a timer ends it at once, and a timer that is not active ignores time.
        /// </summary>
        [Test]
        public void TweenTimer_AfterStop_IsInactiveAndIgnoresTime()
        {
            var timer = new TweenTimer();
            timer.Start(1f);

            timer.Stop();
            timer.Advance(5f);

            Assert.IsFalse(timer.IsActive);
            Assert.AreEqual(0f, timer.Progress, TOLERANCE);
        }

        /// <summary>
        /// The bump is 0 at both ends and 1 in the middle.
        /// </summary>
        [Test]
        public void Bump_OverTheDuration_IsZeroAtTheEndsAndOneInTheMiddle()
        {
            Assert.AreEqual(0f, Tween.Bump(0f), TOLERANCE);
            Assert.AreEqual(1f, Tween.Bump(0.5f), TOLERANCE);
            Assert.AreEqual(0f, Tween.Bump(1f), TOLERANCE);
        }
    }
}
