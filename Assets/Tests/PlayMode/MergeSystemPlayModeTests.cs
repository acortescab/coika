using System.Collections;
using System.Collections.Generic;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the <see cref="MergeSystem"/> against the real physics (issue #7): one result per touching pair,
    /// three-way contacts, chains, the Supernova, the merge position and velocity, and the failure cases. The physics
    /// is stepped by hand (<see cref="MergeTestWorld.UseScriptedPhysics"/>), so the tests are fast and exact.
    /// </summary>
    public class MergeSystemPlayModeTests
    {
        private const float TOLERANCE = 0.001f;
        private const float OVERLAP = 0.98f;
        private const int MAX_STEPS = 30;
        private const int RANDOM_TRIALS = 1000;
        private const int DETERMINISM_DROPS = 24;
        private const int STEPS_BETWEEN_DROPS = 40;
        private const int SETTLE_STEPS = 120;
        private const int SEED = 20260;

        private MergeTestWorld _world;
        private int _merged;
        private int _supernovas;

        /// <summary>
        /// Counts the events of every test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _merged = 0;
            _supernovas = 0;
        }

        /// <summary>
        /// Destroys the world, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _world?.Dispose();
            _world = null;
        }

        /// <summary>
        /// Two same-tier pieces touching produce exactly one piece of the next tier and nothing else.
        /// </summary>
        [Test]
        public void Merge_TwoTouchingPieces_ProducesExactlyOneNextTierPiece()
        {
            StartWorld(4);
            CreateTouchingPair(5, _world.Origin, 0.3f);

            StepFor(MAX_STEPS);

            Assert.AreEqual(1, _merged);
            Assert.AreEqual(0, _world.CountActive(5));
            Assert.AreEqual(1, _world.CountActive(6));
            Assert.AreEqual(1, _world.Factory.ActivePieces.Count);
        }

        /// <summary>
        /// Over 1,000 randomized pairs (tier, place, angle and velocity), every pair produces exactly one next-tier
        /// piece: never zero and never two.
        /// </summary>
        [UnityTest]
        public IEnumerator Merge_With1000RandomizedPairs_AlwaysProducesExactlyOneResult()
        {
            StartWorld(8);
            var random = new System.Random(SEED);
            var jar = _world.Jar;

            for (var trial = 0; trial < RANDOM_TRIALS; trial++)
            {
                var tier = random.Next(0, MergeTestWorld.TIER_COUNT - 2);
                var centre = new Vector2(
                    Mathf.Lerp(jar.InteriorMin.x + 2f, jar.InteriorMax.x - 2f, (float)random.NextDouble()),
                    jar.FloorY + 4f + 4f * (float)random.NextDouble());
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var mergedBefore = _merged;

                CreateTouchingPair(tier, centre, 0.5f, angle, random);
                StepFor(MAX_STEPS);

                Assert.AreEqual(1, _merged - mergedBefore, $"Trial {trial}, tier {tier}: expected exactly one merge.");
                Assert.AreEqual(1, _world.Factory.ActivePieces.Count, $"Trial {trial}, tier {tier}: expected exactly one piece left.");
                Assert.AreEqual(1, _world.CountActive(tier + 1), $"Trial {trial}, tier {tier}: the piece must be of the next tier.");

                _world.Factory.ReleaseAll();
                _world.Merge.ResetForNewRun();
                if (trial % 100 == 99)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Three same-tier pieces touching at once merge only one pair in the first step; the third piece neither
        /// vanishes nor is duplicated.
        /// </summary>
        [Test]
        public void Merge_ThreeTouchingPieces_MergesOnePairAndKeepsTheThird()
        {
            StartWorld(6);
            var radius = TierRadius(2);
            var side = 2f * radius * OVERLAP;
            var a = _world.CreateFloating(2, _world.Origin + new Vector2(-side * 0.5f, 0f), Vector2.zero);
            var b = _world.CreateFloating(2, _world.Origin + new Vector2(side * 0.5f, 0f), Vector2.zero);
            var c = _world.CreateFloating(2, _world.Origin + new Vector2(0f, side * Mathf.Sqrt(3f) * 0.5f), Vector2.zero);

            StepUntilMerged(1);

            Assert.AreEqual(1, _world.CountActive(2), "One of the three is left.");
            Assert.AreEqual(1, _world.CountActive(3));
            Assert.IsTrue(a.Merged && b.Merged, "The two lowest pieces own the merge.");
            Assert.IsFalse(c.Merged);
            Assert.IsTrue(c.gameObject.activeSelf);

            StepFor(MAX_STEPS);
            Assert.AreEqual(1, _merged, "The leftover has nobody to merge with.");
            Assert.AreEqual(2, _world.Factory.ActivePieces.Count);
        }

        /// <summary>
        /// A merge whose new piece touches another piece of its tier merges again, but in a later step, so the chain
        /// is visible. The final board is one piece of the last tier of the chain.
        /// </summary>
        [Test]
        public void Merge_ChainOfTwoSteps_ResolvesOverSuccessiveSteps()
        {
            StartWorld(6);
            var r1 = TierRadius(1);
            var r2 = TierRadius(2);
            var steps = new List<int>();
            var tiers = new List<int>();
            var step = 0;
            _world.Merge.Merged += (tier, _, _) =>
            {
                tiers.Add(tier);
                steps.Add(step);
            };

            // Two tier 1 pieces merge into tier 2 at the origin, where a tier 2 piece is waiting just above.
            _world.CreateFloating(1, _world.Origin + new Vector2(-r1 * OVERLAP, 0f), Vector2.zero);
            _world.CreateFloating(1, _world.Origin + new Vector2(r1 * OVERLAP, 0f), Vector2.zero);
            _world.CreateFloating(2, _world.Origin + new Vector2(0f, 2f * r2 * OVERLAP), Vector2.zero);

            for (step = 0; step < MAX_STEPS; step++)
            {
                _world.Step();
            }

            CollectionAssert.AreEqual(new[] { 2, 3 }, tiers);
            Assert.Greater(steps[1], steps[0], "The second merge must happen in a later step.");
            Assert.AreEqual(1, _world.Factory.ActivePieces.Count);
            Assert.AreEqual(1, _world.CountActive(3));
        }

        /// <summary>
        /// Two Black Holes touching vanish and raise the Supernova once; nothing replaces them and the rest of the
        /// board is untouched.
        /// </summary>
        [Test]
        public void Merge_TwoBlackHoles_VanishAndRaiseSupernovaOnce()
        {
            StartWorld(6);
            var last = MergeTestWorld.TIER_COUNT - 1;
            var radius = TierRadius(last);
            var bystander = _world.CreateFloating(3, _world.Origin + new Vector2(3.5f, 0f), Vector2.zero);
            var position = Vector2.zero;
            _world.Merge.SupernovaTriggered += at => position = at;
            _world.CreateFloating(last, _world.Origin + new Vector2(-radius * OVERLAP, 0f), Vector2.zero);
            _world.CreateFloating(last, _world.Origin + new Vector2(radius * OVERLAP, 0f), Vector2.zero);

            StepFor(MAX_STEPS);

            Assert.AreEqual(1, _supernovas);
            Assert.AreEqual(0, _merged);
            Assert.AreEqual(0, _world.CountActive(last));
            Assert.AreEqual(1, _world.Factory.ActivePieces.Count);
            Assert.IsTrue(bystander.gameObject.activeSelf && !bystander.Merged);
            Assert.AreEqual(_world.Origin.x, position.x, TOLERANCE);
            Assert.AreEqual(_world.Origin.y, position.y, TOLERANCE);
        }

        /// <summary>
        /// The new piece is at the midpoint of the two with their average velocity, no spin, and a spawn grace of
        /// the configured overflow grace.
        /// </summary>
        [Test]
        public void Merge_TwoMovingPieces_CreatesPieceAtMidpointWithAverageVelocity()
        {
            StartWorld(4);
            var radius = TierRadius(3);
            var left = _world.CreateFloating(3, _world.Origin + new Vector2(-radius * OVERLAP, 0f), new Vector2(0.4f, 0.2f));
            var right = _world.CreateFloating(3, _world.Origin + new Vector2(radius * OVERLAP, 0.1f), new Vector2(-0.1f, -0.3f));

            Vector2 mergedPosition = default, mergedVelocity = default;
            _world.Merge.Merged += (_, at, velocity) =>
            {
                mergedPosition = at;
                mergedVelocity = velocity;
            };

            // The merge happens at the start of the step, so the bodies are read just before each merge pass and the
            // new piece is read right after it, before the physics step moves it.
            Vector2 expectedPosition = default, expectedVelocity = default;
            for (var i = 0; i < MAX_STEPS && _merged == 0; i++)
            {
                expectedPosition = (left.Rigidbody.position + right.Rigidbody.position) * 0.5f;
                expectedVelocity = (left.Rigidbody.linearVelocity + right.Rigidbody.linearVelocity) * 0.5f;
                _world.Merge.ProcessQueue();
                if (_merged == 0)
                {
                    Physics2D.Simulate(Time.fixedDeltaTime);
                }
            }

            Assert.AreEqual(1, _merged, "The merge did not happen.");
            var created = _world.Factory.ActivePieces[0];
            Assert.AreEqual(expectedPosition.x, mergedPosition.x, TOLERANCE);
            Assert.AreEqual(expectedPosition.y, mergedPosition.y, TOLERANCE);
            Assert.AreEqual(expectedVelocity.x, mergedVelocity.x, TOLERANCE);
            Assert.AreEqual(expectedVelocity.y, mergedVelocity.y, TOLERANCE);
            Assert.AreEqual(expectedPosition.x, created.Rigidbody.position.x, TOLERANCE);
            Assert.AreEqual(expectedPosition.y, created.Rigidbody.position.y, TOLERANCE);
            Assert.AreEqual(expectedVelocity.x, created.Rigidbody.linearVelocity.x, TOLERANCE);
            Assert.AreEqual(expectedVelocity.y, created.Rigidbody.linearVelocity.y, TOLERANCE);
            Assert.AreEqual(0f, created.Rigidbody.angularVelocity, TOLERANCE);
            Assert.AreEqual(Time.time + _world.Config.OverflowGrace, created.SpawnGraceUntil, TOLERANCE);
            Assert.IsTrue(created.IsInSpawnGrace);
        }

        /// <summary>
        /// Merging with every pooled piece in use grows the pool instead of failing: no exception and one result.
        /// </summary>
        [Test]
        public void Merge_WithPoolExhausted_StillProducesTheResult()
        {
            StartWorld(2);
            CreateTouchingPair(4, _world.Origin, 0f);
            Assert.AreEqual(0, _world.Factory.PooledCount, "Both pooled pieces are in use.");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("pool is exhausted"));

            StepFor(MAX_STEPS);

            Assert.AreEqual(1, _merged);
            Assert.AreEqual(1, _world.CountActive(5));
            Assert.AreEqual(1, _world.Factory.ActivePieces.Count);
        }

        /// <summary>
        /// A pair whose pieces are released after the contact was reported (a restart in the middle of a merge) is
        /// ignored: no merge, no error.
        /// </summary>
        [Test]
        public void Merge_WhenPiecesAreReleasedAfterTheContact_IgnoresThePair()
        {
            StartWorld(4);
            CreateTouchingPair(4, _world.Origin, 0f);
            _world.Step();

            _world.Factory.ReleaseAll();
            StepFor(5);

            Assert.AreEqual(0, _merged);
            Assert.AreEqual(0, _world.Factory.ActivePieces.Count);
        }

        /// <summary>
        /// When the factory cannot create the next tier, one error is logged, both pieces stay untouched and the
        /// error is not repeated every step.
        /// </summary>
        [Test]
        public void Merge_WhenTheFactoryCannotCreate_LogsOneErrorAndLeavesBothPiecesUntouched()
        {
            StartWorld(4);
            var foreignTier = ScriptableObject.CreateInstance<TierDefinition>();
            try
            {
                var tiers = new List<TierDefinition>(_world.Tiers) { [5] = foreignTier };
                _world.Merge.Initialize(_world.Factory, tiers, _world.Config);
                var pair = CreateTouchingPair(4, _world.Origin, 0f);
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("could not create the merged piece"));

                StepFor(10);

                Assert.AreEqual(0, _merged);
                Assert.AreEqual(2, _world.CountActive(4));
                Assert.IsFalse(pair.Item1.Merged || pair.Item2.Merged);
            }
            finally
            {
                UnityEngine.Object.Destroy(foreignTier);
            }
        }

        /// <summary>
        /// The same seed and the same drop positions leave the same board, twice in a row.
        /// </summary>
        [Test]
        public void Merge_SameSeedAndDrops_LeavesTheSameBoardTwice()
        {
            StartWorld(40);
            var first = PlayRun();
            var second = PlayRun();

            Assert.Greater(_merged, 0, "The run should merge something, or the test proves nothing.");
            Assert.AreEqual(first.Count, second.Count, "Same number of pieces.");
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Tier, second[i].Tier, $"Piece {i}: tier.");
                Assert.AreEqual(first[i].Position.x, second[i].Position.x, TOLERANCE, $"Piece {i}: x.");
                Assert.AreEqual(first[i].Position.y, second[i].Position.y, TOLERANCE, $"Piece {i}: y.");
            }
        }

        /// <summary>
        /// Once the pool and the buffers are warm, the merge pass allocates nothing, with merges, chains and resting
        /// contacts of different tiers in play (GDD §14.5). Only <see cref="MergeSystem.ProcessQueue"/> is measured:
        /// the physics step reports the contacts to the queue, but the engine itself may allocate in there.
        /// </summary>
        [Test]
        public void Merge_InSteadyState_AllocatesNothing()
        {
            StartWorld(30);
            CreateAllocationScenario();
            StepFor(MAX_STEPS);

            _world.Factory.ReleaseAll();
            _world.Merge.ResetForNewRun();
            Physics2D.SyncTransforms();
            CreateAllocationScenario();
            var mergedBefore = _merged;
            System.Action process = _world.Merge.ProcessQueue;

            long allocations = 0;
            for (var i = 0; i < MAX_STEPS; i++)
            {
                Physics2D.Simulate(Time.fixedDeltaTime);
                allocations += AllocationMeter.Measure(process);
            }

            Assert.Greater(_merged - mergedBefore, 1, "The measured steps should include merges and a chain.");
            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT, "Allocations in the merge pass.");
        }

        /// <summary>
        /// Places what the allocation test merges: a pair of tier 4, a pair of tier 2 that touches a piece of
        /// tier 3, so the merge starts a chain, and two touching pieces of different tiers that never merge.
        /// </summary>
        private void CreateAllocationScenario()
        {
            var origin = _world.Origin;
            CreateTouchingPair(4, origin + new Vector2(-3f, 0f), 0f);

            var chainCentre = origin + new Vector2(3f, 0f);
            CreateTouchingPair(2, chainCentre, 0f);
            _world.CreateFloating(3, chainCentre + new Vector2(0f, TierRadius(3) * (1f + OVERLAP)), Vector2.zero);

            _world.CreateFloating(1, origin + new Vector2(0f, -2f), Vector2.zero);
            _world.CreateFloating(6, origin + new Vector2(0f, -2f + (TierRadius(1) + TierRadius(6)) * OVERLAP), Vector2.zero);
        }

        /// <summary>
        /// Plays one run: the queue of the seed decides the tiers and a fixed list the drop positions. Returns the
        /// final board in creation order.
        /// </summary>
        private List<(int Tier, Vector2 Position)> PlayRun()
        {
            _world.Factory.ReleaseAll();
            _world.Merge.ResetForNewRun();
            Physics2D.SyncTransforms();

            var queue = new SpawnQueue(_world.Config, SEED);
            var jar = _world.Jar;
            var width = jar.InteriorMax.x - jar.InteriorMin.x - 2f;
            for (var drop = 0; drop < DETERMINISM_DROPS; drop++)
            {
                var x = jar.InteriorMin.x + 1f + width * ((drop * 7) % 11) / 10f;
                _world.Factory.Create(_world.Tiers[queue.Current], new Vector2(x, jar.DropLineY), Vector2.zero);
                queue.Advance();
                StepFor(STEPS_BETWEEN_DROPS);
            }

            StepFor(SETTLE_STEPS);

            var board = new List<(int Tier, Vector2 Position)>();
            var pieces = new List<Piece>(_world.Factory.ActivePieces);
            pieces.Sort((a, b) => a.SequenceId.CompareTo(b.SequenceId));
            foreach (var piece in pieces)
            {
                board.Add((piece.Tier.Index, piece.Rigidbody.position));
            }

            return board;
        }

        /// <summary>
        /// Builds the world, hooks the event counters and takes over the physics.
        /// </summary>
        /// <param name="prewarmCount">Pieces in the pool.</param>
        private void StartWorld(int prewarmCount)
        {
            _world = new MergeTestWorld(prewarmCount);
            var start = _world.StartAsync();
            Assert.IsTrue(start.IsCompleted && !start.IsFaulted, start.Exception?.ToString());
            _world.Merge.Merged += (_, _, _) => _merged++;
            _world.Merge.SupernovaTriggered += _ => _supernovas++;
            _world.UseScriptedPhysics();
        }

        /// <summary>
        /// Creates two floating pieces of a tier that overlap a little, so they touch at the first physics step.
        /// </summary>
        /// <param name="tier">Tier index of both pieces.</param>
        /// <param name="centre">Point between the two pieces.</param>
        /// <param name="speed">Largest speed component of each piece, in either direction. 0 for none.</param>
        /// <param name="angle">Direction of the line between the pieces, in radians.</param>
        /// <param name="random">Source of the velocities. May be null when the speed is 0.</param>
        private (Piece, Piece) CreateTouchingPair(int tier, Vector2 centre, float speed, float angle = 0f, System.Random random = null)
        {
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (TierRadius(tier) * OVERLAP);
            var first = _world.CreateFloating(tier, centre - offset, RandomVelocity(speed, random));
            var second = _world.CreateFloating(tier, centre + offset, RandomVelocity(speed, random));
            return (first, second);
        }

        /// <summary>
        /// A velocity with each component between -speed and speed.
        /// </summary>
        private static Vector2 RandomVelocity(float speed, System.Random random)
        {
            if (random == null || speed == 0f)
            {
                return Vector2.zero;
            }

            return new Vector2((float)(random.NextDouble() * 2.0 - 1.0), (float)(random.NextDouble() * 2.0 - 1.0)) * speed;
        }

        /// <summary>
        /// Radius of a tier of the test theme.
        /// </summary>
        private float TierRadius(int tier)
        {
            return _world.Tiers[tier].DiameterUnits * 0.5f;
        }

        /// <summary>
        /// Steps the physics and the merge system a number of times.
        /// </summary>
        /// <param name="count">Number of steps.</param>
        private void StepFor(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _world.Step();
            }
        }

        /// <summary>
        /// Steps until the given number of merges happened, failing if they do not within a few steps.
        /// </summary>
        /// <param name="merges">Merges to wait for.</param>
        private void StepUntilMerged(int merges)
        {
            for (var i = 0; i < MAX_STEPS && _merged < merges; i++)
            {
                _world.Step();
            }

            Assert.GreaterOrEqual(_merged, merges, "The merge did not happen.");
        }
    }
}
