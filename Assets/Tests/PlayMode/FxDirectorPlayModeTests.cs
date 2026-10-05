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
        private const float FALL_HEIGHT = 8f;

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
            _director.Bind(_world.Merge, _world.Score, _world.Factory, new Vector2(0f, _world.Jar.DangerLineY));
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
        /// Merging two pieces of tiers 0 to 9 emits the burst in the colour of the created tier with a count within
        /// the configured range, at the position of the new piece, and one white flash ring.
        /// </summary>
        [Test]
        public void Merge_OfEveryTier_EmitsBurstInTierColourAndFlashRing()
        {
            for (var tier = 0; tier < _world.Tiers.Count - 1; tier++)
            {
                _spawner.Requests.Clear();
                MergeTwo(tier);

                var burst = _spawner.Single(FxKind.MergeBurst);
                Assert.AreEqual(_world.Tiers[tier + 1].TierColor, burst.Color, $"Colour of tier {tier + 1}.");
                Assert.AreEqual(_feedback.MergeBurstCount(tier + 1, _world.Tiers.Count), burst.Count, $"Count of tier {tier + 1}.");
                Assert.GreaterOrEqual(burst.Count, _feedback.MergeBurstMinCount);
                Assert.LessOrEqual(burst.Count, _feedback.MergeBurstMaxCount);

                var ring = _spawner.Single(FxKind.FlashRing);
                Assert.AreEqual(Color.white, ring.Color);
                Assert.AreEqual(burst.Position, ring.Position, "The ring and the burst start at the same place.");
                Assert.That(burst.Position.y, Is.GreaterThan(_world.Jar.FloorY), "The merge happens inside the jar.");
            }
        }

        /// <summary>
        /// Two Black Holes touching emit the white flash and the shockwave ring at the midpoint.
        /// </summary>
        [Test]
        public void Supernova_OfTwoBlackHoles_EmitsFlashAndShockwave()
        {
            MergeTwo(_world.Tiers.Count - 1);

            var flash = _spawner.Single(FxKind.SupernovaFlash);
            var shockwave = _spawner.Single(FxKind.Shockwave);
            Assert.AreEqual(Color.white, flash.Color);
            Assert.AreEqual(flash.Position, shockwave.Position);
            Assert.That(flash.Position.y, Is.GreaterThan(_world.Jar.FloorY));
        }

        /// <summary>
        /// The first score above the best emits the confetti once, at the origin the director was given.
        /// </summary>
        [Test]
        public void NewBest_FirstScoreAboveBest_EmitsConfettiOnce()
        {
            MergeTwo(0);
            MergeTwo(1);

            var confetti = _spawner.Single(FxKind.Confetti);
            Assert.AreEqual(_feedback.ConfettiCount, confetti.Count);
            Assert.AreEqual(new Vector2(0f, _world.Jar.DangerLineY), confetti.Position);
        }

        /// <summary>
        /// A piece that falls from high up and lands hard emits a dust puff at the floor, under the piece.
        /// </summary>
        [Test]
        public void Landing_AboveTheThreshold_EmitsDustUnderThePiece()
        {
            var piece = _world.Factory.Create(_world.Tiers[2], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
            StepUntil(() => _spawner.Count(FxKind.LandingDust) > 0);

            var dust = _spawner.First(FxKind.LandingDust);
            Assert.AreEqual(_feedback.LandDustCount, dust.Count);
            Assert.That(dust.Position.y, Is.LessThan(piece.transform.position.y), "The dust is under the piece.");
            Assert.That(dust.Position.y, Is.EqualTo(_world.Jar.FloorY).Within(0.5f), "The dust is at the floor.");
        }

        /// <summary>
        /// A director that is unbound reacts to nothing.
        /// </summary>
        [Test]
        public void Unbind_ThenMerge_EmitsNothing()
        {
            _director.Unbind();
            _spawner.Requests.Clear();

            MergeTwo(0);

            Assert.AreEqual(0, _spawner.Requests.Count);
        }

        /// <summary>
        /// Creates two overlapping pieces of a tier and steps until they merge.
        /// </summary>
        /// <param name="tier">Tier of the two pieces.</param>
        private void MergeTwo(int tier)
        {
            _world.Factory.ReleaseAll();
            var radius = _world.Tiers[tier].Radius;
            var y = _world.Jar.FloorY + 6f;
            var before = _world.Score.Merges;
            _world.Factory.Create(_world.Tiers[tier], new Vector2(-radius * 0.45f, y), Vector2.zero);
            _world.Factory.Create(_world.Tiers[tier], new Vector2(radius * 0.45f, y), Vector2.zero);
            StepUntil(() => _world.Score.Merges > before || _spawner.Count(FxKind.SupernovaFlash) > 0);
        }

        /// <summary>
        /// Steps the world until the condition holds.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        private void StepUntil(System.Func<bool> condition)
        {
            for (var i = 0; i < MAX_STEPS && !condition(); i++)
            {
                _world.Step();
            }

            Assert.IsTrue(condition(), "The expected event did not happen.");
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
            public Request First(FxKind kind)
            {
                return Requests.Find(request => request.Kind == kind);
            }

            /// <summary>
            /// Asserts that exactly one request of a kind was made and returns it.
            /// </summary>
            /// <param name="kind">The effect.</param>
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
