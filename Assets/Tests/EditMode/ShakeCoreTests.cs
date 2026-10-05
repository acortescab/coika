using System;
using Coika.Fx;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the maths of the screen shake (<see cref="ShakeCore"/>): whole 1/16 steps, the cap, the exact return to
    /// zero, the slots and the determinism, all on a fake clock.
    /// </summary>
    public class ShakeCoreTests
    {
        private const float STEP = 1f / 16f;
        private const float MAX = 0.3f;
        private const double FRAME = 1.0 / 60.0;
        private const int SEED = 5;

        /// <summary>
        /// Every offset during a long shake is a multiple of 1/16 on both axes.
        /// </summary>
        [Test]
        public void Evaluate_DuringAShake_IsAlwaysAMultipleOfOneSixteenth()
        {
            var core = new ShakeCore(4, MAX, SEED);
            core.Add(0.15f, 0.2f, 0.0);

            for (var now = 0.0; now < 0.25; now += FRAME)
            {
                var offset = core.Evaluate(now);
                AssertWholeSteps(offset.x);
                AssertWholeSteps(offset.y);
            }
        }

        /// <summary>
        /// After the duration the offset is exactly zero and stays zero.
        /// </summary>
        [Test]
        public void Evaluate_AfterTheDuration_IsExactlyZero()
        {
            var core = new ShakeCore(4, MAX, SEED);
            core.Add(0.15f, 0.2f, 0.0);

            Assert.AreEqual(Vector2.zero, core.Evaluate(0.2));
            Assert.AreEqual(Vector2.zero, core.Evaluate(5.0));
            Assert.AreEqual(0, core.CountActive(0.0));
        }

        /// <summary>
        /// Without any shake the offset is zero.
        /// </summary>
        [Test]
        public void Evaluate_WithoutShakes_IsZero()
        {
            Assert.AreEqual(Vector2.zero, new ShakeCore(4, MAX, SEED).Evaluate(1.0));
        }

        /// <summary>
        /// Many strong shakes at once never move the camera past the cap on either axis.
        /// </summary>
        [Test]
        public void Evaluate_WithManyShakes_NeverExceedsTheCap()
        {
            var core = new ShakeCore(8, MAX, SEED);
            for (var i = 0; i < 20; i++)
            {
                core.Add(0.5f, 0.3f, 0.0);
            }

            for (var now = 0.0; now < 0.3; now += FRAME)
            {
                var offset = core.Evaluate(now);
                Assert.LessOrEqual(Mathf.Abs(offset.x), MAX + 0.0001f);
                Assert.LessOrEqual(Mathf.Abs(offset.y), MAX + 0.0001f);
            }
        }

        /// <summary>
        /// When every slot is busy a new shake takes the slot that ends first, so the count never passes the slots.
        /// </summary>
        [Test]
        public void Add_WhenFull_ReplacesTheShakeThatEndsFirst()
        {
            var core = new ShakeCore(2, MAX, SEED);
            core.Add(0.1f, 0.5f, 0.0);
            core.Add(0.1f, 0.1f, 0.0);

            core.Add(0.1f, 0.5f, 0.0);

            Assert.AreEqual(2, core.CountActive(0.0));
            Assert.AreEqual(2, core.CountActive(0.2), "The short shake was replaced by a long one, so both still run.");
        }

        /// <summary>
        /// The same requests on the same seed give the same offsets.
        /// </summary>
        [Test]
        public void Evaluate_WithTheSameSeed_IsDeterministic()
        {
            var a = new ShakeCore(4, MAX, SEED);
            var b = new ShakeCore(4, MAX, SEED);
            a.Add(0.2f, 0.2f, 0.0);
            b.Add(0.2f, 0.2f, 0.0);

            for (var now = 0.0; now < 0.2; now += FRAME)
            {
                Assert.AreEqual(a.Evaluate(now), b.Evaluate(now));
            }
        }

        /// <summary>
        /// Clearing stops the shake at once.
        /// </summary>
        [Test]
        public void Clear_DuringAShake_ReturnsToZero()
        {
            var core = new ShakeCore(4, MAX, SEED);
            core.Add(0.2f, 1f, 0.0);

            core.Clear();

            Assert.AreEqual(Vector2.zero, core.Evaluate(0.1));
        }

        /// <summary>
        /// A shake with no amplitude or no duration is ignored.
        /// </summary>
        [Test]
        public void Add_WithZeroAmplitudeOrDuration_IsIgnored()
        {
            var core = new ShakeCore(4, MAX, SEED);

            core.Add(0f, 1f, 0.0);
            core.Add(1f, 0f, 0.0);

            Assert.AreEqual(0, core.CountActive(0.0));
        }

        /// <summary>
        /// The constructor rejects no slots and a negative cap.
        /// </summary>
        [Test]
        public void Constructor_WithBadArguments_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShakeCore(0, MAX, SEED));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShakeCore(1, -1f, SEED));
        }

        /// <summary>
        /// Snap rounds to the nearest 1/16 and never returns negative zero.
        /// </summary>
        [Test]
        public void Snap_OfSmallNegativeValue_IsPositiveZero()
        {
            Assert.AreEqual(0f, ShakeCore.Snap(-0.01f));
            Assert.IsFalse(float.IsNegative(ShakeCore.Snap(-0.01f)));
            Assert.AreEqual(STEP, ShakeCore.Snap(0.05f));
        }

        /// <summary>
        /// Asserts that a length is a whole number of 1/16 steps.
        /// </summary>
        /// <param name="value">Length in world units.</param>
        private static void AssertWholeSteps(float value)
        {
            Assert.AreEqual(Mathf.Round(value / STEP) * STEP, value, 0f, "Not a multiple of 1/16.");
        }
    }
}
