using Coika.Core;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests the smooth music duck (issue #29).
    /// </summary>
    public class DuckEnvelopeTests
    {
        /// <summary>
        /// A duck over half a second reaches -6 dB at the end and not before.
        /// </summary>
        [Test]
        public void Advance_TowardTheTarget_ReachesItAfterTheTime()
        {
            var duck = new DuckEnvelope();
            duck.SetTarget(-6f, 0.5f);

            duck.Advance(0.25f);
            Assert.AreEqual(-3f, duck.Current, 0.001f);

            duck.Advance(0.5f);
            Assert.AreEqual(-6f, duck.Current, 0.001f);
        }

        /// <summary>
        /// Restoring with 0 dB ramps back up and never overshoots.
        /// </summary>
        [Test]
        public void Advance_BackToZero_RampsUpWithoutOvershoot()
        {
            var duck = new DuckEnvelope();
            duck.SetTarget(-6f, 0f);
            duck.SetTarget(0f, 0.3f);

            duck.Advance(0.15f);
            Assert.AreEqual(-3f, duck.Current, 0.001f);

            duck.Advance(1f);
            Assert.AreEqual(0f, duck.Current);
        }

        /// <summary>
        /// A duration of zero applies the target at once.
        /// </summary>
        [Test]
        public void SetTarget_WithZeroSeconds_AppliesAtOnce()
        {
            var duck = new DuckEnvelope();
            duck.SetTarget(-6f, 0f);

            Assert.AreEqual(-6f, duck.Current);
            Assert.IsFalse(duck.Advance(1f));
        }

        /// <summary>
        /// A positive target counts as 0: the music is never boosted.
        /// </summary>
        [Test]
        public void SetTarget_Positive_CountsAsZero()
        {
            var duck = new DuckEnvelope();
            duck.SetTarget(3f, 0f);

            Assert.AreEqual(0f, duck.Current);
        }
    }
}
