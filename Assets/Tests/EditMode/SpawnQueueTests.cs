using System;
using System.Linq;
using System.Text.RegularExpressions;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="SpawnQueue"/> against the acceptance criteria of issue #5: determinism, the forced opening,
    /// the anti-streak rule, the weights, the spawnable range, the Current and Next contract and zero allocations.
    /// </summary>
    public class SpawnQueueTests
    {
        private const int SEED = TestSpawnSettings.SEED;

        /// <summary>
        /// Reads a queue: the current tier, then the result of each advance, so the array is the sequence of
        /// pieces in the order they are dropped.
        /// </summary>
        /// <param name="queue">The queue to read.</param>
        /// <param name="count">Number of pieces.</param>
        private static int[] Sequence(SpawnQueue queue, int count)
        {
            var tiers = new int[count];
            tiers[0] = queue.Current;
            for (int i = 1; i < count; i++)
            {
                tiers[i] = queue.Advance();
            }

            return tiers;
        }

        /// <summary>
        /// Length of the longest run of the same tier in a sequence.
        /// </summary>
        /// <param name="tiers">The sequence.</param>
        private static int LongestRun(int[] tiers)
        {
            var longest = 1;
            var run = 1;
            for (int i = 1; i < tiers.Length; i++)
            {
                run = tiers[i] == tiers[i - 1] ? run + 1 : 1;
                longest = Math.Max(longest, run);
            }

            return longest;
        }

        /// <summary>
        /// A new queue already holds the current and the next tier, and the seed it was given.
        /// </summary>
        [Test]
        public void Constructor_WithValidSettings_FillsCurrentNextAndSeed()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);

            Assert.AreEqual(0, queue.Current);
            Assert.AreEqual(1, queue.Next);
            Assert.AreEqual(SEED, queue.Seed);
        }

        /// <summary>
        /// A queue built from a config takes its spawn values.
        /// </summary>
        [Test]
        public void Constructor_WithAConfig_UsesItsSpawnValues()
        {
            var config = TestGameConfig.CreateWithSpawn(new[] { 1f, 1f, 1f }, 3, 2, new[] { 2, 1 });
            try
            {
                var queue = new SpawnQueue(config, SEED);

                Assert.AreEqual(2, queue.Current);
                Assert.AreEqual(1, queue.Next);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        /// <summary>
        /// A missing config or missing settings is a programming error and throws.
        /// </summary>
        [Test]
        public void Constructor_WithNullArguments_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SpawnQueue((GameConfig)null, SEED), "config");
            Assert.Throws<ArgumentNullException>(() => new SpawnQueue((SpawnSettings)null, SEED), "settings");
        }

        /// <summary>
        /// A config with invalid spawn values gives a clear ArgumentException, not a hang or a loop later. The
        /// config's OnValidate logs the problem too, which the test expects.
        /// </summary>
        [Test]
        public void Constructor_WithAConfigWithInvalidValues_ThrowsAClearError()
        {
            LogAssert.Expect(LogType.Error, new Regex("above zero"));
            var config = TestGameConfig.CreateWithSpawn(new[] { 0f, 0f, 0f }, 3, 3, new int[0]);
            try
            {
                var exception = Assert.Throws<ArgumentException>(() => new SpawnQueue(config, SEED));

                StringAssert.Contains("above zero", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        /// <summary>
        /// Same seed and same settings give the same 10,000 tiers.
        /// </summary>
        [Test]
        public void Advance_WithTheSameSeedAndSettings_ProducesTheSameSequenceOver10000()
        {
            var first = Sequence(new SpawnQueue(TestSpawnSettings.Default(), SEED), 10000);
            var second = Sequence(new SpawnQueue(TestSpawnSettings.Default(), SEED), 10000);

            CollectionAssert.AreEqual(first, second);
        }

        /// <summary>
        /// Different seeds give different sequences, so the seed really drives the result.
        /// </summary>
        [Test]
        public void Advance_WithDifferentSeeds_ProducesDifferentSequences()
        {
            var first = Sequence(new SpawnQueue(TestSpawnSettings.Default(), 1), 200);
            var second = Sequence(new SpawnQueue(TestSpawnSettings.Default(), 2), 200);

            CollectionAssert.AreNotEqual(first, second);
        }

        /// <summary>
        /// The first three pieces are 0, 1, 0 whatever the seed.
        /// </summary>
        [Test]
        public void Advance_FirstThreePieces_AreTheForcedOpeningForAnySeed()
        {
            for (int seed = -50; seed < 250; seed++)
            {
                var queue = new SpawnQueue(TestSpawnSettings.Default(), seed);

                CollectionAssert.AreEqual(new[] { 0, 1, 0 }, Sequence(queue, 3), $"seed {seed}");
            }
        }

        /// <summary>
        /// Advance returns the new current tier, and what was Next becomes Current.
        /// </summary>
        [Test]
        public void Advance_Always_MakesNextTheCurrentAndReturnsIt()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);

            for (int i = 0; i < 1000; i++)
            {
                var expected = queue.Next;

                var returned = queue.Advance();

                Assert.AreEqual(expected, returned, $"advance {i}: returned value");
                Assert.AreEqual(expected, queue.Current, $"advance {i}: Current");
            }
        }

        /// <summary>
        /// Over 100,000 pieces no tier appears more than three times in a row.
        /// </summary>
        [Test]
        public void Advance_Over100000Pieces_NeverRepeatsATierMoreThanThreeTimes()
        {
            var tiers = Sequence(new SpawnQueue(TestSpawnSettings.Default(), SEED), 100000);

            Assert.LessOrEqual(LongestRun(tiers), 3);
        }

        /// <summary>
        /// With one tier almost certain, the streak is still capped and the other tier is forced in.
        /// </summary>
        [Test]
        public void Advance_WithADominantWeight_CapsTheStreakAndForcesTheOtherTier()
        {
            var tiers = Sequence(new SpawnQueue(TestSpawnSettings.For(new[] { 100f, 1f }, 2), SEED), 50000);

            Assert.LessOrEqual(LongestRun(tiers), 2);
            Assert.Contains(1, tiers, "The other tier must appear.");
        }

        /// <summary>
        /// The forced opening counts toward the streak: after opening 0, 0, 0 with a maximum of three, the fourth
        /// piece cannot be 0 even though that tier is almost certain.
        /// </summary>
        [Test]
        public void Advance_AfterAForcedOpeningAtTheStreakLimit_BreaksTheStreak()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var queue = new SpawnQueue(TestSpawnSettings.For(new[] { 100f, 1f }, 3, 0, 0, 0), seed);

                Assert.AreEqual(1, Sequence(queue, 4)[3], $"seed {seed}");
            }
        }

        /// <summary>
        /// The forced opening is never re-rolled: it is produced exactly, even against the weights.
        /// </summary>
        [Test]
        public void Advance_ForcedOpening_IsNeverRerolled()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var queue = new SpawnQueue(TestSpawnSettings.For(new[] { 100f, 1f }, 3, 1, 1, 1), seed);

                CollectionAssert.AreEqual(new[] { 1, 1, 1 }, Sequence(queue, 3), $"seed {seed}");
            }
        }

        /// <summary>
        /// With no forced opening the very first piece is already random, so different seeds start differently.
        /// </summary>
        [Test]
        public void Constructor_WithAnEmptyOpening_StartsRandomImmediately()
        {
            var firstTiers = Enumerable.Range(0, 100)
                .Select(seed => new SpawnQueue(TestSpawnSettings.For(TestSpawnSettings.DefaultWeights, 3), seed).Current)
                .Distinct()
                .ToList();

            Assert.Greater(firstTiers.Count, 1, "The first piece must not be fixed.");
        }

        /// <summary>
        /// Over 100,000 pieces after the opening, each tier appears within 1.5 percentage points of its weight.
        /// </summary>
        [Test]
        public void Advance_Over100000Pieces_MatchesTheWeightsWithinOnePointFivePoints()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);
            queue.Advance();
            queue.Advance(); // The forced opening is over: from here every piece is drawn.
            var counts = new int[5];
            const int total = 100000;

            for (int i = 0; i < total; i++)
            {
                counts[queue.Advance()]++;
            }

            for (int tier = 0; tier < 5; tier++)
            {
                var expected = TestSpawnSettings.DefaultWeights[tier] / 100.0;
                Assert.AreEqual(expected, counts[tier] / (double)total, 0.015, $"tier {tier}");
            }
        }

        /// <summary>
        /// Tiers outside the spawnable range are never produced.
        /// </summary>
        [Test]
        public void Advance_WithFewerSpawnableTiers_NeverProducesTiersOutsideTheRange()
        {
            var tiers = Sequence(new SpawnQueue(TestSpawnSettings.For(new[] { 5f, 3f, 2f }, 3), SEED), 20000);

            Assert.IsTrue(tiers.All(tier => tier >= 0 && tier <= 2));
        }

        /// <summary>
        /// A tier with no weight is never produced.
        /// </summary>
        [Test]
        public void Advance_WithAZeroWeightTier_NeverProducesIt()
        {
            var tiers = Sequence(new SpawnQueue(TestSpawnSettings.For(new[] { 3f, 0f, 3f }, 3), SEED), 20000);

            CollectionAssert.DoesNotContain(tiers, 1);
        }

        /// <summary>
        /// A single spawnable tier cannot satisfy the anti-streak rule, so it repeats; it must not hang.
        /// </summary>
        [Test, Timeout(5000)]
        public void Advance_WithASingleSpawnableTier_RepeatsWithoutHanging()
        {
            var tiers = Sequence(new SpawnQueue(TestSpawnSettings.For(new[] { 1f }, 3), SEED), 1000);

            Assert.IsTrue(tiers.All(tier => tier == 0));
        }

        /// <summary>
        /// If only the tier on a streak has any weight, it repeats instead of looping forever.
        /// </summary>
        [Test, Timeout(5000)]
        public void Advance_WhenOnlyTheStreakingTierHasWeight_RepeatsWithoutHanging()
        {
            var tiers = Sequence(new SpawnQueue(TestSpawnSettings.For(new[] { 1f, 0f }, 1), SEED), 1000);

            Assert.IsTrue(tiers.All(tier => tier == 0));
        }

        /// <summary>
        /// After construction, advancing allocates no managed memory.
        /// </summary>
        [Test]
        public void Advance_AfterConstruction_AllocatesNoManagedMemory()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);
            for (int i = 0; i < 100; i++)
            {
                queue.Advance();
            }

            var allocated = AllocationMeter.Measure(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    queue.Advance();
                }
            });

            Assert.LessOrEqual(allocated, AllocationMeter.TOLERANCE_COUNT, "Managed allocations made by 10,000 advances.");
        }

        /// <summary>
        /// The allocation meter really sees allocations: code that allocates on every call is above the tolerance,
        /// so a result below it in the zero-allocation tests is a measurement and not a counter that never moves.
        /// The objects are kept alive so the garbage collector cannot free them during the measure.
        /// </summary>
        [Test]
        public void AllocationMeter_WithCodeThatAllocatesEveryCall_ReportsMoreThanTheTolerance()
        {
            var kept = new object[10000];

            var allocated = AllocationMeter.Measure(() =>
            {
                for (int i = 0; i < kept.Length; i++)
                {
                    kept[i] = new int[8];
                }
            });

            GC.KeepAlive(kept);

            Assert.Greater(allocated, AllocationMeter.TOLERANCE_COUNT);
        }

        /// <summary>
        /// Advance raises Advanced once, and the event handler already sees the new Current and Next.
        /// </summary>
        [Test]
        public void Advance_Always_RaisesAdvancedOnceAfterTheStateIsUpdated()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);
            var calls = 0;
            var currentSeenByTheHandler = -1;
            queue.Advanced += () =>
            {
                calls++;
                currentSeenByTheHandler = queue.Current;
            };
            var expectedCurrent = queue.Next;

            queue.Advance();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(expectedCurrent, currentSeenByTheHandler);
        }
    }
}
