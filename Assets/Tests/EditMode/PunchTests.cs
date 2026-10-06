using System;
using Coika.UI;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="Punch"/> on a fake clock: idle at 1, a peak half way and a return to exactly 1.
    /// </summary>
    public class PunchTests
    {
        private const float DURATION = 0.2f;
        private const float PEAK = 1.5f;

        private double _now;

        /// <summary>
        /// Rewinds the fake clock.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _now = 0d;
        }

        /// <summary>
        /// Builds a punch on the fake clock.
        /// </summary>
        private Punch Create(float duration = DURATION)
        {
            return new Punch(() => _now, duration, PEAK);
        }

        /// <summary>
        /// A null clock is rejected.
        /// </summary>
        [Test]
        public void Constructor_WithANullClock_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Punch(null, DURATION, PEAK));
        }

        /// <summary>
        /// An idle punch is at the resting scale.
        /// </summary>
        [Test]
        public void Evaluate_WhenIdle_IsOne()
        {
            Assert.That(Create().Evaluate(), Is.EqualTo(1f));
        }

        /// <summary>
        /// The punch reaches the peak half way.
        /// </summary>
        [Test]
        public void Evaluate_HalfWay_IsThePeak()
        {
            var punch = Create();
            punch.Start();

            _now = DURATION / 2d;

            Assert.That(punch.Evaluate(), Is.EqualTo(PEAK).Within(0.001f));
            Assert.That(punch.Running, Is.True);
        }

        /// <summary>
        /// The punch ends at exactly 1 and stops.
        /// </summary>
        [Test]
        public void Evaluate_AfterTheDuration_IsOneAndStops()
        {
            var punch = Create();
            punch.Start();

            _now = DURATION;

            Assert.That(punch.Evaluate(), Is.EqualTo(1f));
            Assert.That(punch.Running, Is.False);
        }

        /// <summary>
        /// Cancel stops the punch at once.
        /// </summary>
        [Test]
        public void Cancel_DuringAPunch_ReturnsToOne()
        {
            var punch = Create();
            punch.Start();
            _now = DURATION / 2d;

            punch.Cancel();

            Assert.That(punch.Evaluate(), Is.EqualTo(1f));
        }

        /// <summary>
        /// A zero duration disables the punch.
        /// </summary>
        [Test]
        public void Start_WithAZeroDuration_DoesNotRun()
        {
            var punch = Create(0f);

            punch.Start();

            Assert.That(punch.Running, Is.False);
        }

        /// <summary>
        /// The scale of the reduced-motion setting is 1 normally and smaller when it is on.
        /// </summary>
        [Test]
        public void MotionScale_WithReducedMotion_IsSmaller()
        {
            Assert.That(UiAnimation.MotionScale(false), Is.EqualTo(1f));
            Assert.That(UiAnimation.MotionScale(true), Is.LessThan(1f).And.GreaterThan(0f));
        }
    }
}
