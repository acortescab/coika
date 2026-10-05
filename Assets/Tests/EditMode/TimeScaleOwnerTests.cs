using System;
using Coika.Fx;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="TimeScaleOwner"/> with a fake time scale and a fake unscaled clock, so the real
    /// <c>Time.timeScale</c> is never touched: the slow-mo lasts real seconds, and it never overrides the pause.
    /// </summary>
    public class TimeScaleOwnerTests
    {
        private FakeTimeScale _scale;
        private double _now;
        private TimeScaleOwner _owner;

        /// <summary>
        /// Builds an owner on a fake scale that starts wrong, to see it reset to 1.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _scale = new FakeTimeScale { Value = 0.3f };
            _now = 100.0;
            _owner = new TimeScaleOwner(_scale, () => _now);
        }

        /// <summary>
        /// Creating the owner puts the scale to 1.
        /// </summary>
        [Test]
        public void Constructor_Always_SetsTheScaleToOne()
        {
            Assert.AreEqual(1f, _scale.Value);
        }

        /// <summary>
        /// A slow-mo sets the scale, and ends after its real seconds whatever the scale.
        /// </summary>
        [Test]
        public void Begin_ThenTick_RestoresAfterTheDuration()
        {
            _owner.Begin(0.7f, 0.1f);
            Assert.AreEqual(0.7f, _scale.Value);

            _now += 0.09;
            _owner.Tick();
            Assert.AreEqual(0.7f, _scale.Value, "Not over yet.");

            _now += 0.02;
            _owner.Tick();
            Assert.AreEqual(1f, _scale.Value);
            Assert.IsFalse(_owner.IsSlowMo);
        }

        /// <summary>
        /// Pausing during a slow-mo sets 0, cancels the slow-mo, and resuming gives 1, not 0.7.
        /// </summary>
        [Test]
        public void Paused_DuringSlowMo_ResumesAtNormalSpeed()
        {
            _owner.Begin(0.7f, 0.1f);

            _owner.Paused = true;
            Assert.AreEqual(0f, _scale.Value);

            _now += 0.5;
            _owner.Tick();
            Assert.AreEqual(0f, _scale.Value, "The end of the slow-mo does not touch the paused scale.");

            _owner.Paused = false;
            Assert.AreEqual(1f, _scale.Value);
        }

        /// <summary>
        /// A slow-mo requested while paused is refused.
        /// </summary>
        [Test]
        public void Begin_WhilePaused_IsIgnored()
        {
            _owner.Paused = true;

            _owner.Begin(0.7f, 0.1f);

            Assert.AreEqual(0f, _scale.Value);
            Assert.IsFalse(_owner.IsSlowMo);
        }

        /// <summary>
        /// Cancelling while paused keeps the scale at 0.
        /// </summary>
        [Test]
        public void Cancel_WhilePaused_KeepsTheScaleAtZero()
        {
            _owner.Paused = true;

            _owner.Cancel();

            Assert.AreEqual(0f, _scale.Value);
        }

        /// <summary>
        /// Cancelling a running slow-mo gives the normal speed back.
        /// </summary>
        [Test]
        public void Cancel_DuringSlowMo_RestoresOne()
        {
            _owner.Begin(0.7f, 1f);

            _owner.Cancel();

            Assert.AreEqual(1f, _scale.Value);
        }

        /// <summary>
        /// The constructor rejects a null dependency.
        /// </summary>
        [Test]
        public void Constructor_WithNulls_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TimeScaleOwner(null, () => 0.0));
            Assert.Throws<ArgumentNullException>(() => new TimeScaleOwner(_scale, null));
        }

        /// <summary>
        /// A time scale held in a field.
        /// </summary>
        private sealed class FakeTimeScale : ITimeScale
        {
            /// <summary>The scale.</summary>
            public float Value { get; set; }
        }
    }
}
