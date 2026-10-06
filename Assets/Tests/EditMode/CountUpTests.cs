using System;
using Coika.UI;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="CountUp"/> on a fake clock: it rolls over the duration, ends on the exact target, jumps
    /// when asked and allocates nothing while it ticks.
    /// </summary>
    public class CountUpTests
    {
        private const float DURATION = 0.3f;

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
        /// Builds a counter on the fake clock.
        /// </summary>
        private CountUp Create(float duration = DURATION)
        {
            return new CountUp(() => _now, duration);
        }

        /// <summary>
        /// A null clock is rejected.
        /// </summary>
        [Test]
        public void Constructor_WithANullClock_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CountUp(null, DURATION));
        }

        /// <summary>
        /// The value moves part of the way at half the duration.
        /// </summary>
        [Test]
        public void Tick_AtHalfTheDuration_IsHalfWay()
        {
            var count = Create();
            count.SetTarget(1000);

            _now = DURATION / 2d;

            Assert.That(count.Tick(), Is.True);
            Assert.That(count.Value, Is.EqualTo(500).Within(1));
            Assert.That(count.Running, Is.True);
        }

        /// <summary>
        /// It ends on the exact target at the duration and stops.
        /// </summary>
        [TestCase(DURATION)]
        [TestCase(DURATION * 10f)]
        public void Tick_AtOrAfterTheDuration_EndsOnTheExactTarget(float elapsed)
        {
            var count = Create();
            count.SetTarget(123457);

            _now = elapsed;
            count.Tick();

            Assert.That(count.Value, Is.EqualTo(123457));
            Assert.That(count.Running, Is.False);
        }

        /// <summary>
        /// A new target during a roll starts from the value shown.
        /// </summary>
        [Test]
        public void SetTarget_DuringARoll_StartsFromTheShownValue()
        {
            var count = Create();
            count.SetTarget(1000);
            _now = DURATION / 2d;
            count.Tick();
            var shown = count.Value;

            count.SetTarget(2000);
            count.Tick();

            Assert.That(count.Value, Is.EqualTo(shown));
            _now += DURATION;
            count.Tick();
            Assert.That(count.Value, Is.EqualTo(2000));
        }

        /// <summary>
        /// Snap jumps to the value and cancels the roll.
        /// </summary>
        [Test]
        public void Snap_DuringARoll_ShowsTheValueAndStops()
        {
            var count = Create();
            count.SetTarget(1000);

            count.Snap(1000);

            Assert.That(count.Value, Is.EqualTo(1000));
            Assert.That(count.Running, Is.False);
            Assert.That(count.Tick(), Is.False);
        }

        /// <summary>
        /// A zero duration makes every change instant.
        /// </summary>
        [Test]
        public void SetTarget_WithAZeroDuration_JumpsAtOnce()
        {
            var count = Create(0f);

            count.SetTarget(77);

            Assert.That(count.Value, Is.EqualTo(77));
            Assert.That(count.Running, Is.False);
        }

        /// <summary>
        /// A score can also go down (a new run), and the roll ends on the exact lower value.
        /// </summary>
        [Test]
        public void SetTarget_WithALowerValue_EndsOnIt()
        {
            var count = Create();
            count.Snap(500);

            count.SetTarget(0);
            _now = DURATION;
            count.Tick();

            Assert.That(count.Value, Is.EqualTo(0));
        }

        /// <summary>
        /// Ticking a roll allocates nothing.
        /// </summary>
        [Test]
        public void Tick_WhileRolling_AllocatesNothing()
        {
            var count = Create();
            count.SetTarget(1000);
            _now = DURATION / 4d;
            count.Tick();

            var allocations = AllocationMeter.Measure(() =>
            {
                _now += 0.01d;
                count.Tick();
            });

            Assert.That(allocations, Is.LessThanOrEqualTo(AllocationMeter.TOLERANCE_COUNT));
        }
    }
}
