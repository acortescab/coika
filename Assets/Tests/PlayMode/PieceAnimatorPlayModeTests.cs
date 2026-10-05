using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the visual piece animations (issue #31) with exact time steps: no frame passes in these tests, so
    /// every duration is checked by calling <see cref="PieceAnimator.Tick"/> with a fixed step, like the fake clock of
    /// the harness. The animator must end each animation within one step of the duration in <see cref="FeedbackConfig"/>
    /// and must never touch the root transform or the collider.
    /// </summary>
    public class PieceAnimatorPlayModeTests : HarnessTestBase
    {
        private const float STEP = 1f / 60f;
        private const float TOLERANCE = 0.0001f;

        private SimulationWorld _world;
        private FeedbackConfig _feedback;
        private Piece _piece;
        private PieceAnimator _animator;

        /// <summary>
        /// Builds a world with animations and one new piece, whose spawn pop is running.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _world = new SimulationWorld(new SimulationOptions { Seed = 1 });
            _world.StartRun();
            _feedback = _world.Config.Feedback;
            _piece = _world.Factory.Create(_world.Tiers[0], new Vector2(0f, 2f), Vector2.zero);
            _animator = _piece.Animator;
        }

        /// <summary>
        /// Gives the world back.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        /// <summary>
        /// Ticks until the animation ends or the limit passes.
        /// </summary>
        /// <param name="seconds">Longest time to tick.</param>
        /// <returns>Seconds ticked until the animator stopped, or the limit.</returns>
        private float TickUntilRest(float seconds)
        {
            var elapsed = 0f;
            while (_animator.IsRunning && elapsed < seconds)
            {
                _animator.Tick(STEP);
                elapsed += STEP;
            }

            return elapsed;
        }

        /// <summary>
        /// A new piece starts at scale zero, overshoots past one and ends at exactly one in the spawn duration.
        /// </summary>
        [Test]
        public void Spawn_AfterCreate_GrowsFromZeroWithOvershootAndEndsAtOneInTheDuration()
        {
            Assert.AreEqual(0f, _animator.VisualScale.x, TOLERANCE, "It starts at scale 0.");

            var max = 0f;
            var elapsed = 0f;
            while (_animator.IsRunning)
            {
                _animator.Tick(STEP);
                elapsed += STEP;
                max = Mathf.Max(max, _animator.VisualScale.x);
            }

            Assert.AreEqual(_feedback.SpawnDuration, elapsed, STEP + TOLERANCE, "Within one frame of the duration.");
            Assert.Greater(max, 1f, "It overshoots.");
            Assert.AreEqual(Vector3.one, _animator.VisualScale, "It ends at exactly one.");
        }

        /// <summary>
        /// The merge pop goes 0, up to the peak, then back to one, in the merge pop duration.
        /// </summary>
        [Test]
        public void MergePop_AfterAMerge_PeaksAtTheConfiguredScaleAndEndsAtOneInTheDuration()
        {
            TickUntilRest(1f);
            _animator.Play(PieceEffectId.MergePop);
            Assert.AreEqual(0f, _animator.VisualScale.x, TOLERANCE, "It starts at scale 0.");

            var max = 0f;
            var elapsed = 0f;
            while (_animator.IsRunning)
            {
                _animator.Tick(STEP);
                elapsed += STEP;
                max = Mathf.Max(max, _animator.VisualScale.x);
            }

            Assert.AreEqual(_feedback.MergePopDuration, elapsed, STEP + TOLERANCE, "Within one frame of the duration.");
            Assert.AreEqual(_feedback.MergePopPeak, max, 0.02f, "It peaks at the configured scale.");
            Assert.AreEqual(Vector3.one, _animator.VisualScale);
        }

        /// <summary>
        /// A landing below the impulse threshold does not squash the piece.
        /// </summary>
        [Test]
        public void Land_BelowTheThreshold_DoesNothing()
        {
            TickUntilRest(1f);

            _animator.Play(PieceEffectId.Land, _feedback.LandImpulseThreshold * 0.5f);

            Assert.IsFalse(_animator.IsRunning);
            Assert.AreEqual(Vector3.one, _animator.VisualScale);
        }

        /// <summary>
        /// A landing above the threshold squashes for the land duration and returns to exactly one.
        /// </summary>
        [Test]
        public void Land_AboveTheThreshold_SquashesForTheDurationAndReturnsToOne()
        {
            TickUntilRest(1f);

            _animator.Play(PieceEffectId.Land, _feedback.LandImpulseThreshold + 1f);
            Assert.IsTrue(_animator.IsRunning);

            var elapsed = TickUntilRest(1f);

            Assert.AreEqual(_feedback.LandDuration, elapsed, STEP + TOLERANCE, "Within one frame of the duration.");
            Assert.AreEqual(Vector3.one, _animator.VisualScale);
        }

        /// <summary>
        /// The squash grows with the impulse and is clamped at the maximum amplitude.
        /// </summary>
        [Test]
        public void Land_WithGrowingImpulse_GrowsTheAmplitudeUntilTheClamp()
        {
            var small = MaxSquashFor(_feedback.LandImpulseThreshold + 1f);
            var large = MaxSquashFor(_feedback.LandImpulseThreshold + 4f);
            var huge = MaxSquashFor(1000f);
            var hugest = MaxSquashFor(2000f);

            Assert.Greater(large, small, "A harder landing squashes more.");
            Assert.AreEqual(huge, hugest, TOLERANCE, "The amplitude is clamped.");
            Assert.LessOrEqual(huge, 1f + _feedback.LandAmplitudeMax + TOLERANCE);
        }

        /// <summary>
        /// A piece that falls is stretched vertically and the stretch ends when it lands.
        /// </summary>
        [Test]
        public void Drop_WhileFalling_StretchesUntilItLands()
        {
            TickUntilRest(1f);
            _piece.Rigidbody.linearVelocity = new Vector2(0f, -_feedback.DropStretchFullSpeed);

            _animator.Play(PieceEffectId.Drop);
            _animator.Tick(STEP);

            Assert.Greater(_animator.VisualScale.y, 1f, "It is taller while falling.");
            Assert.Less(_animator.VisualScale.x, 1f, "It is narrower while falling.");
            Assert.IsTrue(_animator.IsRunning);

            _animator.Play(PieceEffectId.Land, 0f);

            Assert.IsFalse(_animator.IsRunning);
            Assert.AreEqual(Vector3.one, _animator.VisualScale);
        }

        /// <summary>
        /// The animations only scale the visual child: the root scale, the collider and the body never change.
        /// </summary>
        [Test]
        public void Animations_WhileRunning_NeverChangeTheRootOrTheCollider()
        {
            var radius = _piece.Collider.radius;
            var mass = _piece.Rigidbody.mass;

            _animator.Play(PieceEffectId.MergePop);
            _animator.Play(PieceEffectId.Land, _feedback.LandImpulseThreshold + 3f);
            for (var i = 0; i < 20; i++)
            {
                _animator.Tick(STEP);
                Assert.AreEqual(Vector3.one, _piece.transform.localScale, "Root scale");
                Assert.AreEqual(radius, _piece.Collider.radius, "Collider radius");
                Assert.AreEqual(mass, _piece.Rigidbody.mass, "Mass");
            }

            Assert.AreNotSame(_piece.transform, _animator.transform, "The animator is on a child.");
        }

        /// <summary>
        /// A piece that goes back to the pool in the middle of an animation is at scale one at once, and a reused
        /// piece starts a clean spawn pop.
        /// </summary>
        [Test]
        public void Release_WhileAnimating_ResetsTheVisualAndReuseStartsClean()
        {
            _animator.Play(PieceEffectId.MergePop);
            _animator.Tick(STEP);
            Assert.IsTrue(_animator.IsRunning);

            _world.Factory.Release(_piece);

            Assert.IsFalse(_animator.IsRunning);
            Assert.AreEqual(Vector3.one, _animator.VisualScale);

            var reused = _world.Factory.Create(_world.Tiers[1], Vector2.zero, Vector2.zero);
            Assert.AreSame(_piece, reused, "The pool hands the same piece back.");
            Assert.AreEqual(0f, _animator.VisualScale.x, TOLERANCE, "A new spawn pop starts.");

            TickUntilRest(1f);
            Assert.AreEqual(Vector3.one, _animator.VisualScale);
        }

        /// <summary>
        /// Without a feedback config the animator is inert and the visual stays at rest.
        /// </summary>
        [Test]
        public void Spawn_WithoutAFeedbackConfig_StaysAtRest()
        {
            _animator.Begin(_piece, null);

            _animator.Play(PieceEffectId.Spawn);
            _animator.Play(PieceEffectId.MergePop);
            _animator.Play(PieceEffectId.Drop);
            _animator.Play(PieceEffectId.Land, 100f);

            Assert.IsFalse(_animator.IsRunning);
            Assert.AreEqual(Vector3.one, _animator.VisualScale);
        }

        /// <summary>
        /// Ticking the running animations allocates nothing.
        /// </summary>
        [Test]
        public void Tick_WhileAnimating_AllocatesNothing()
        {
            _animator.Play(PieceEffectId.MergePop);
            _animator.Play(PieceEffectId.Land, _feedback.LandImpulseThreshold + 3f);
            _animator.Play(PieceEffectId.Drop);
            _animator.Tick(STEP);

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < 30; i++)
                {
                    _animator.Tick(STEP);
                }
            });

            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT);
        }

        /// <summary>
        /// Plays a landing of the given impulse on a piece at rest and returns how wide the visual gets.
        /// </summary>
        /// <param name="impulse">Impulse of the landing.</param>
        /// <returns>The largest horizontal scale, which squash widens.</returns>
        private float MaxSquashFor(float impulse)
        {
            TickUntilRest(1f);
            _animator.Play(PieceEffectId.Land, impulse);

            var max = 0f;
            while (_animator.IsRunning)
            {
                _animator.Tick(STEP);
                max = Mathf.Max(max, _animator.VisualScale.x);
            }

            return max;
        }
    }
}
