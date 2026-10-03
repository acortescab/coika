using System;
using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the <see cref="RunTimer"/> (issue #10): it counts from the start, freezes at the stop and restarts,
    /// all on a clock the test moves.
    /// </summary>
    public class RunTimerTests
    {
        private const float TOLERANCE = 0.0001f;

        private double _now;
        private RunTimer _timer;

        /// <summary>
        /// Creates a stopped timer whose clock the test moves through <c>_now</c>.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _now = 100d;
            _timer = new RunTimer(() => _now);
        }

        /// <summary>
        /// Before the first start there is nothing to measure.
        /// </summary>
        [Test]
        public void ElapsedSeconds_BeforeStart_IsZero()
        {
            _now = 150d;

            Assert.That(_timer.ElapsedSeconds, Is.EqualTo(0f).Within(TOLERANCE));
        }

        /// <summary>
        /// While it runs, the elapsed time follows the clock from the start.
        /// </summary>
        [Test]
        public void ElapsedSeconds_WhileRunning_FollowsTheClock()
        {
            _timer.Start();
            _now += 12.5d;

            Assert.That(_timer.ElapsedSeconds, Is.EqualTo(12.5f).Within(TOLERANCE));
        }

        /// <summary>
        /// After the stop the elapsed time no longer moves with the clock.
        /// </summary>
        [Test]
        public void ElapsedSeconds_AfterStop_IsFrozen()
        {
            _timer.Start();
            _now += 30d;
            _timer.Stop();
            _now += 500d;

            Assert.That(_timer.ElapsedSeconds, Is.EqualTo(30f).Within(TOLERANCE));
        }

        /// <summary>
        /// A second stop keeps the first stop time.
        /// </summary>
        [Test]
        public void Stop_CalledTwice_KeepsTheFirstStopTime()
        {
            _timer.Start();
            _now += 10d;
            _timer.Stop();
            _now += 10d;
            _timer.Stop();

            Assert.That(_timer.ElapsedSeconds, Is.EqualTo(10f).Within(TOLERANCE));
        }

        /// <summary>
        /// Starting again counts from 0 again.
        /// </summary>
        [Test]
        public void Start_AfterAStop_RestartsFromZero()
        {
            _timer.Start();
            _now += 40d;
            _timer.Stop();
            _now += 5d;

            _timer.Start();
            _now += 2d;

            Assert.That(_timer.ElapsedSeconds, Is.EqualTo(2f).Within(TOLERANCE));
        }

        /// <summary>
        /// A timer needs a clock.
        /// </summary>
        [Test]
        public void Constructor_WithNullClock_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RunTimer(null));
        }
    }
}
