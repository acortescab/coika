using System;
using System.Collections.Generic;
using Coika.Data;
using Coika.Fx;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks that <see cref="FxDirector"/> asks for each effect of issue #32 at the right position, with the right
    /// colour and count, for every tier, using a recording spawner instead of real particles.
    /// </summary>
    public class FxDirectorPlayModeTests : HarnessTestBase
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
        private RecordingSpawner _spawner;
        private FxDirector _director;

        /// <summary>
        /// Builds a world without real particles, whose tiers have a distinct colour each, and a director that
        /// records its requests.
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
            _spawner = new RecordingSpawner();
            _director = new FxDirector(_spawner, _feedback, _world.Tiers);
            _world.StartRun();
            _director.Bind(_world.Merge, _world.Score, _world.Factory, ConfettiOrigin());
        }

        /// <summary>
        /// Gives the world back.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _director.Unbind();
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
                Assert.AreEqual(_feedback.MergeBurstCount(tier + 1, _world.Tiers.Count), burst.Count, $"Count of tier {tier + 1}.");
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
        /// The first score above the best emits the confetti once, at the origin the director was given, and a
        /// second merge does not emit it again.
        /// </summary>
        [Test]
        public void NewBest_FirstScoreAboveBest_EmitsConfettiOnce()
        {
            MergeFresh(0);
            var confetti = _spawner.Single(FxKind.Confetti);

            MergeFresh(1);

            Assert.AreEqual(_feedback.ConfettiCount, confetti.Count);
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
            TestReflection.SetField(_feedback, "_landImpulseThreshold", ANY_LANDING_THRESHOLD);

            for (var tier = 0; tier < _world.Tiers.Count; tier++)
            {
                _world.Factory.ReleaseAll();
                _spawner.Requests.Clear();
                _world.Factory.Create(_world.Tiers[tier], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
                StepUntil(() => _spawner.Count(FxKind.LandingDust) > 0);

                var dust = _spawner.First(FxKind.LandingDust);
                var expected = Color.Lerp(_world.Tiers[tier].TierColor, Color.white, DUST_WHITE_BLEND);
                Assert.AreEqual(expected, dust.Color, $"Dust colour of tier {tier}.");
                Assert.AreEqual(_feedback.LandDustCount, dust.Count);
                Assert.AreEqual(_world.Jar.FloorY, dust.Position.y, POSITION_TOLERANCE, $"The dust of tier {tier} is at the floor.");
            }
        }

        /// <summary>
        /// A landing below the impulse threshold emits no dust.
        /// </summary>
        [Test]
        public void Landing_BelowTheThreshold_EmitsNoDust()
        {
            TestReflection.SetField(_feedback, "_landImpulseThreshold", HUGE_THRESHOLD);
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
            _director.Bind(_world.Merge, _world.Score, _world.Factory, ConfettiOrigin());

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
            TestReflection.SetField(_feedback, "_landImpulseThreshold", ANY_LANDING_THRESHOLD);
            _world.Factory.Create(_world.Tiers[3], new Vector2(5f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
            _director.Unbind();
            _spawner.Requests.Clear();

            MergeFresh(0);
            StepFor(RESTING_STEPS);

            Assert.AreEqual(0, _spawner.Requests.Count);
        }

        /// <summary>
        /// A director needs every dependency, and binding needs every system.
        /// </summary>
        [Test]
        public void Constructor_AndBind_WithNulls_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new FxDirector(null, _feedback, _world.Tiers));
            Assert.Throws<ArgumentNullException>(() => new FxDirector(_spawner, null, _world.Tiers));
            Assert.Throws<ArgumentNullException>(() => new FxDirector(_spawner, _feedback, null));
            Assert.Throws<ArgumentNullException>(() => _director.Bind(null, _world.Score, _world.Factory, Vector2.zero));
            Assert.Throws<ArgumentNullException>(() => _director.Bind(_world.Merge, null, _world.Factory, Vector2.zero));
            Assert.Throws<ArgumentNullException>(() => _director.Bind(_world.Merge, _world.Score, null, Vector2.zero));
        }

        /// <summary>
        /// The handlers of every event allocate nothing, called a thousand times each with a spawner that does nothing.
        /// </summary>
        [Test]
        public void Handlers_CalledManyTimes_AllocateNothing()
        {
            _director.Unbind();
            var quiet = new FxDirector(new QuietSpawner(), _feedback, _world.Tiers);
            quiet.Bind(_world.Merge, _world.Score, _world.Factory, ConfettiOrigin());
            var piece = _world.Factory.Create(_world.Tiers[2], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
            var merged = (Action<int, Vector2, Vector2>)TestReflection.GetField(_world.Merge, "Merged");
            var supernova = (Action<Vector2>)TestReflection.GetField(_world.Merge, "SupernovaTriggered");
            var newBest = (Action)TestReflection.GetField(_world.Score, "NewBestReached");
            var landed = (Action<Piece, float>)TestReflection.GetField(piece, "Landed");
            var impulse = _feedback.LandImpulseThreshold + 1f;

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < HANDLER_CALLS; i++)
                {
                    merged(3, Vector2.zero, Vector2.zero);
                    supernova(Vector2.zero);
                    newBest();
                    landed(piece, impulse);
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
            return new Vector2(0f, _world.Jar.DangerLineY);
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
        /// A spawner that ignores every request.
        /// </summary>
        private sealed class QuietSpawner : IParticleSpawner
        {
            /// <inheritdoc />
            public void Burst(FxKind kind, Vector2 position, Color color, int count)
            {
            }
        }

        /// <summary>
        /// A spawner that records every request.
        /// </summary>
        private sealed class RecordingSpawner : IParticleSpawner
        {
            /// <summary>Every request, in order.</summary>
            public List<Request> Requests { get; } = new();

            /// <inheritdoc />
            public void Burst(FxKind kind, Vector2 position, Color color, int count)
            {
                Requests.Add(new Request(kind, position, color, count));
            }

            /// <summary>
            /// Counts the requests of a kind.
            /// </summary>
            /// <param name="kind">The effect.</param>
            /// <returns>The number of requests of that kind.</returns>
            public int Count(FxKind kind)
            {
                var count = 0;
                foreach (var request in Requests)
                {
                    if (request.Kind == kind)
                    {
                        count++;
                    }
                }

                return count;
            }

            /// <summary>
            /// Returns the first request of a kind.
            /// </summary>
            /// <param name="kind">The effect.</param>
            /// <returns>The first request of that kind, or null.</returns>
            public Request First(FxKind kind)
            {
                return Requests.Find(request => request.Kind == kind);
            }

            /// <summary>
            /// Asserts that exactly one request of a kind was made and returns it.
            /// </summary>
            /// <param name="kind">The effect.</param>
            /// <returns>The only request of that kind.</returns>
            public Request Single(FxKind kind)
            {
                Assert.AreEqual(1, Count(kind), $"Expected exactly one {kind}.");
                return First(kind);
            }
        }

        /// <summary>
        /// One recorded request.
        /// </summary>
        private sealed class Request
        {
            /// <summary>Creates the record.</summary>
            /// <param name="kind">The effect.</param>
            /// <param name="position">Where it was requested.</param>
            /// <param name="color">The colour.</param>
            /// <param name="count">The requested count.</param>
            public Request(FxKind kind, Vector2 position, Color color, int count)
            {
                Kind = kind;
                Position = position;
                Color = color;
                Count = count;
            }

            /// <summary>The effect.</summary>
            public FxKind Kind { get; }

            /// <summary>Where it was requested.</summary>
            public Vector2 Position { get; }

            /// <summary>The colour.</summary>
            public Color Color { get; }

            /// <summary>The requested count.</summary>
            public int Count { get; }
        }
    }
}
