using System;
using Coika.Core;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests the rate limit of the landing sounds (issue #29) with a fake clock.
    /// </summary>
    public class LandLimiterTests
    {
        private double _now;
        private LandLimiter _limiter;

        /// <summary>
        /// Creates a limiter that reads the fake clock.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _now = 0.0; // Starts at 0 so adding exactly 0.04 stays exact in floating point
            _limiter = new LandLimiter(() => _now);
        }

        /// <summary>
        /// The first request is accepted.
        /// </summary>
        [Test]
        public void TryAcquire_FirstRequest_IsAccepted()
        {
            Assert.IsTrue(_limiter.TryAcquire(0.1));
        }

        /// <summary>
        /// A second request within 40 ms is dropped.
        /// </summary>
        [Test]
        public void TryAcquire_WithinFortyMilliseconds_IsDropped()
        {
            _limiter.TryAcquire(0.1);
            _now += 0.039;

            Assert.IsFalse(_limiter.TryAcquire(0.1));
        }

        /// <summary>
        /// A request 40 ms after the last accepted one is accepted.
        /// </summary>
        [Test]
        public void TryAcquire_AfterFortyMilliseconds_IsAccepted()
        {
            _limiter.TryAcquire(0.1);
            _now += 0.04;

            Assert.IsTrue(_limiter.TryAcquire(0.1));
        }

        /// <summary>
        /// A dropped request does not delay the next one: the interval counts from the last accepted sound.
        /// </summary>
        [Test]
        public void TryAcquire_AfterADroppedRequest_CountsFromTheAcceptedOne()
        {
            _limiter.TryAcquire(0.1);
            _now += 0.03;
            _limiter.TryAcquire(0.1);
            _now += 0.011;

            Assert.IsTrue(_limiter.TryAcquire(0.1));
        }

        /// <summary>
        /// A fourth landing sound is dropped while three are still sounding, and accepted once one has ended.
        /// </summary>
        [Test]
        public void TryAcquire_WithThreeSounding_DropsTheFourth()
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(_limiter.TryAcquire(0.5), $"sound {i}");
                _now += 0.05;
            }

            Assert.IsFalse(_limiter.TryAcquire(0.5));

            _now += 0.4; // The first one ended
            Assert.IsTrue(_limiter.TryAcquire(0.5));
        }

        /// <summary>
        /// A limiter needs a clock.
        /// </summary>
        [Test]
        public void Constructor_WithoutAClock_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new LandLimiter(null));
        }
    }
}
