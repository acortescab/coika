using System;
using Coika.Data;
using Coika.Fx;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks that <see cref="FeedbackDirector"/> asks for each particle effect of issue #32 at the right position,
    /// with the right colour and count, for every tier, using a recording spawner instead of real particles.
    /// </summary>
    public class FeedbackParticlesPlayModeTests : HarnessTestBase
    {
        private const int MAX_STEPS = 600;
        private const int RESTING_STEPS = 240;
        private const float FALL_HEIGHT = 8f;
        private const float DUST_WHITE_BLEND = 0.6f;
        private const float POSITION_TOLERANCE = 0.25f;
        private const float HUGE_THRESHOLD = 100000f;
        private const float ANY_LANDING_THRESHOLD = 0.0001f;
        private const int HANDLER_CALLS = 1000;

        private SimulationWorld _world;
        private FeedbackConfig _feedback;
        private FakeParticleSpawner _spawner;

        /// <summary>
        /// Builds a world without real particles, whose tiers have a distinct colour each, with the director bound
        /// to a recording spawner.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _world = new SimulationWorld(new SimulationOptions { Seed = 1, Particles = false });
            for (var i = 0; i < _world.Tiers.Count; i++)
            {
                TestReflection.SetField(_world.Tiers[i], "_tierColor", new Color(i / 10f, 1f - i / 10f, 0.5f, 1f));
            }

            _feedback = _world.Config.Feedback;
            _spawner = _world.FakeParticles;
            _world.StartRun();
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
        /// Merging two pieces of tiers 0 to 9 emits the burst in the colour of the created tier with the count of
        /// that tier, at the middle of the pair, and one white flash ring at the same place.
        /// </summary>
        [Test]
        public void Merge_OfEveryTier_EmitsBurstInTierColourAndFlashRing()
        {
            for (var tier = 0; tier < _world.Tiers.Count - 1; tier++)
            {
                MergeFresh(tier);

                var burst = _spawner.Single(FxKind.MergeBurst);
                Assert.AreEqual(_world.Tiers[tier + 1].TierColor, burst.Color, $"Colour of tier {tier + 1}.");
                Assert.AreEqual(_feedback.Particles.MergeBurstCount(tier + 1, _world.Tiers.Count), burst.Count, $"Count of tier {tier + 1}.");
                Assert.AreEqual(0f, burst.Position.x, POSITION_TOLERANCE, "The burst is at the middle of the pair.");
                Assert.AreEqual(MergeScenario.PairHeight(_world), burst.Position.y, POSITION_TOLERANCE);

                var ring = _spawner.Single(FxKind.FlashRing);
                Assert.AreEqual(Color.white, ring.Color);
                Assert.AreEqual(burst.Position, ring.Position, "The ring and the burst start at the same place.");
            }
        }

        /// <summary>
        /// Two Black Holes touching emit the white flash and the shockwave ring at their midpoint.
        /// </summary>
        [Test]
        public void Supernova_OfTwoBlackHoles_EmitsFlashAndShockwave()
        {
            MergeFresh(_world.Tiers.Count - 1);

            var flash = _spawner.Single(FxKind.SupernovaFlash);
            var shockwave = _spawner.Single(FxKind.Shockwave);
            Assert.AreEqual(Color.white, flash.Color);
            Assert.AreEqual(flash.Position, shockwave.Position);
            Assert.AreEqual(0f, flash.Position.x, POSITION_TOLERANCE);
            Assert.AreEqual(MergeScenario.PairHeight(_world), flash.Position.y, POSITION_TOLERANCE);
        }

        /// <summary>
        /// The first score above the best emits the confetti once, at the middle of the Danger Line, and a second
        /// merge does not emit it again.
        /// </summary>
        [Test]
        public void NewBest_FirstScoreAboveBest_EmitsConfettiOnce()
        {
            MergeFresh(0);
            var confetti = _spawner.Single(FxKind.Confetti);

            MergeFresh(1);

            Assert.AreEqual(_feedback.Particles.ConfettiCount, confetti.Count);
            Assert.AreEqual(ConfettiOrigin(), confetti.Position);
            Assert.AreEqual(0, _spawner.Count(FxKind.Confetti), "The second merge is not a new best again.");
        }

        /// <summary>
        /// A run whose score never passes the best emits no confetti.
        /// </summary>
        [Test]
        public void NewBest_WhenTheBestIsOutOfReach_EmitsNoConfetti()
        {
            _world.Score.BestScore = int.MaxValue;

            MergeFresh(0);

            Assert.AreEqual(0, _spawner.Count(FxKind.Confetti));
        }

        /// <summary>
        /// For every tier, a landing emits a dust puff at the floor in the tier colour lightened towards white, with
        /// the configured count.
        /// </summary>
        [Test]
        public void Landing_OfEveryTier_EmitsLightenedDustAtTheFloor()
        {
            TestReflection.SetField(_feedback.Animations, "_landImpulseThreshold", ANY_LANDING_THRESHOLD);

            for (var tier = 0; tier < _world.Tiers.Count; tier++)
            {
                _world.Factory.ReleaseAll();
                _spawner.Requests.Clear();
                _world.Factory.Create(_world.Tiers[tier], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
                StepUntil(() => _spawner.Count(FxKind.LandingDust) > 0);

                var dust = _spawner.First(FxKind.LandingDust);
                var expected = Color.Lerp(_world.Tiers[tier].TierColor, Color.white, DUST_WHITE_BLEND);
                Assert.AreEqual(expected, dust.Color, $"Dust colour of tier {tier}.");
                Assert.AreEqual(_feedback.Particles.LandDustCount, dust.Count);
                Assert.AreEqual(_world.Jar.FloorY, dust.Position.y, POSITION_TOLERANCE, $"The dust of tier {tier} is at the floor.");
            }
        }

        /// <summary>
        /// A landing below the impulse threshold emits no dust.
        /// </summary>
        [Test]
        public void Landing_BelowTheThreshold_EmitsNoDust()
        {
            TestReflection.SetField(_feedback.Animations, "_landImpulseThreshold", HUGE_THRESHOLD);
            var piece = _world.Factory.Create(_world.Tiers[2], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);

            StepFor(RESTING_STEPS);

            Assert.Less(piece.transform.position.y, _world.Jar.FloorY + _world.Tiers[2].DiameterUnits, "The piece should have landed.");
            Assert.AreEqual(0, _spawner.Count(FxKind.LandingDust));
        }

        /// <summary>
        /// Binding twice to the same systems never makes an effect appear twice.
        /// </summary>
        [Test]
        public void Bind_Twice_EmitsEachEffectOnce()
        {
            BindWorldDirector(_world.Feedback);

            MergeFresh(0);

            Assert.AreEqual(1, _spawner.Count(FxKind.MergeBurst));
            Assert.AreEqual(1, _spawner.Count(FxKind.FlashRing));
        }

        /// <summary>
        /// A director that is unbound reacts to no merge and to no landing of the pieces in play.
        /// </summary>
        [Test]
        public void Unbind_ThenMergeAndLand_EmitsNothing()
        {
            TestReflection.SetField(_feedback.Animations, "_landImpulseThreshold", ANY_LANDING_THRESHOLD);
            _world.Factory.Create(_world.Tiers[3], new Vector2(5f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
            _world.Feedback.Unbind();
            _spawner.Requests.Clear();

            MergeFresh(0);
            StepFor(RESTING_STEPS);

            Assert.AreEqual(0, _spawner.Requests.Count);
        }

        /// <summary>
        /// A director needs every dependency but the audio and the haptics, and binding needs every system.
        /// </summary>
        [Test]
        public void Constructor_AndBind_WithNulls_Throw()
        {
            var fx = _world.ScreenFx;
            Func<double> clock = () => 0.0;
            var tiers = _world.Tiers;
            Assert.Throws<ArgumentNullException>(() => new FeedbackDirector(null, null, null, fx, fx, fx, _feedback, tiers, clock));
            Assert.Throws<ArgumentNullException>(() => new FeedbackDirector(null, null, _spawner, null, fx, fx, _feedback, tiers, clock));
            Assert.Throws<ArgumentNullException>(() => new FeedbackDirector(null, null, _spawner, fx, null, fx, _feedback, tiers, clock));
            Assert.Throws<ArgumentNullException>(() => new FeedbackDirector(null, null, _spawner, fx, fx, null, _feedback, tiers, clock));
            Assert.Throws<ArgumentNullException>(() => new FeedbackDirector(null, null, _spawner, fx, fx, fx, null, tiers, clock));
            Assert.Throws<ArgumentNullException>(() => new FeedbackDirector(null, null, _spawner, fx, fx, fx, _feedback, null, clock));
            Assert.Throws<ArgumentNullException>(() => new FeedbackDirector(null, null, _spawner, fx, fx, fx, _feedback, tiers, null));
            Assert.DoesNotThrow(() => new FeedbackDirector(null, null, _spawner, fx, fx, fx, _feedback, tiers, clock));

            var w = _world;
            Assert.Throws<ArgumentNullException>(() => w.Feedback.Bind(null, w.Score, w.Controller, w.Overflow, w.Factory, w.Manager, w.Jar));
            Assert.Throws<ArgumentNullException>(() => w.Feedback.Bind(w.Merge, null, w.Controller, w.Overflow, w.Factory, w.Manager, w.Jar));
            Assert.Throws<ArgumentNullException>(() => w.Feedback.Bind(w.Merge, w.Score, null, w.Overflow, w.Factory, w.Manager, w.Jar));
            Assert.Throws<ArgumentNullException>(() => w.Feedback.Bind(w.Merge, w.Score, w.Controller, null, w.Factory, w.Manager, w.Jar));
            Assert.Throws<ArgumentNullException>(() => w.Feedback.Bind(w.Merge, w.Score, w.Controller, w.Overflow, null, w.Manager, w.Jar));
            Assert.Throws<ArgumentNullException>(() => w.Feedback.Bind(w.Merge, w.Score, w.Controller, w.Overflow, w.Factory, null, w.Jar));
            Assert.Throws<ArgumentNullException>(() => w.Feedback.Bind(w.Merge, w.Score, w.Controller, w.Overflow, w.Factory, w.Manager, null));
        }

        /// <summary>
        /// The handlers of every event allocate nothing, called a thousand times each with fakes that do nothing.
        /// </summary>
        [Test]
        public void Handlers_CalledManyTimes_AllocateNothing()
        {
            _world.Feedback.Unbind();
            var quiet = new FeedbackDirector(
                null, null, NullParticleSpawner.Instance, NullScreenEffects.Instance, new QuietSlowMo(), NullScreenEffects.Instance,
                _feedback, _world.Tiers, () => 0.0);
            BindWorldDirector(quiet);
            var piece = _world.Factory.Create(_world.Tiers[2], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
            var merged = (Action<int, Vector2, Vector2>)TestReflection.GetField(_world.Merge, "Merged");
            var supernova = (Action<Vector2>)TestReflection.GetField(_world.Merge, "SupernovaTriggered");
            var newBest = (Action)TestReflection.GetField(_world.Score, "NewBestReached");
            var landed = (Action<Piece, float>)TestReflection.GetField(piece, "Landed");
            var impulse = _feedback.Animations.LandImpulseThreshold + 1f;

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < HANDLER_CALLS; i++)
                {
                    merged(3, Vector2.zero, Vector2.zero);
                    supernova(Vector2.zero);
                    newBest();
                    landed(piece, impulse);
                    quiet.Tick();
                }
            });

            quiet.Unbind();
            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT);
        }

        /// <summary>
        /// Where the confetti starts in these tests.
        /// </summary>
        /// <returns>The middle of the Danger Line.</returns>
        private Vector2 ConfettiOrigin()
        {
            return new Vector2(_world.Jar.transform.position.x, _world.Jar.DangerLineY);
        }

        /// <summary>
        /// Binds a director to the systems of the world.
        /// </summary>
        /// <param name="director">The director to bind.</param>
        private void BindWorldDirector(FeedbackDirector director)
        {
            director.Bind(_world.Merge, _world.Score, _world.Controller, _world.Overflow, _world.Factory, _world.Manager, _world.Jar);
        }

        /// <summary>
        /// Forgets the earlier pieces and requests, then merges two pieces of the tier.
        /// </summary>
        /// <param name="tier">Tier of the two pieces.</param>
        private void MergeFresh(int tier)
        {
            _world.Factory.ReleaseAll();
            _spawner.Requests.Clear();
            MergeScenario.MergeTwo(_world, tier, () => _spawner.Count(FxKind.SupernovaFlash) > 0);
        }

        /// <summary>
        /// Steps the world a number of times.
        /// </summary>
        /// <param name="steps">Number of physics steps.</param>
        private void StepFor(int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                _world.Step();
            }
        }

        /// <summary>
        /// Steps the world until the condition holds.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        private void StepUntil(Func<bool> condition)
        {
            for (var i = 0; i < MAX_STEPS && !condition(); i++)
            {
                _world.Step();
            }

            Assert.IsTrue(condition(), "The expected event did not happen.");
        }

        /// <summary>
        /// A slow-mo that ignores every request.
        /// </summary>
        private sealed class QuietSlowMo : ISlowMo
        {
            /// <inheritdoc />
            public void Begin(float scale, float duration)
            {
            }

            /// <inheritdoc />
            public void Cancel()
            {
            }
        }
    }
}
