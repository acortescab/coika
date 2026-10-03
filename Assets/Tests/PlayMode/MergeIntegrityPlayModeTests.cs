using System.Diagnostics;
using NUnit.Framework;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Merge integrity across the whole game (GDD §3.4, §19): 1,000 randomized drops per seed, checking after every
    /// physics step that the board only changes by drops and merges, that each merge removes two pieces and adds
    /// one (a Supernova removes two), and that no piece flagged as merged is still on the board. Games that end
    /// restart through Retry, so every seed plays the full 1,000 drops. It uses the asset service double.
    /// </summary>
    public class MergeIntegrityPlayModeTests : HarnessTestBase
    {
        private const int DROP_COUNT = 1000;
        private const float COOLDOWN_SECONDS = 0.1f;
        private const float SETTLE_SECONDS = 2f;
        private const int MIN_EXPECTED_MERGES = 50;
        private const double MAX_REAL_SECONDS = 60.0;

        /// <summary>
        /// For each seed, the piece count of the board follows <c>previous + drops - merges - 2 * supernovas</c> at
        /// every step, and a piece on the board is never flagged as merged.
        /// </summary>
        /// <param name="seed">Seed of the run, also used for the drop positions.</param>
        [Test]
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(42)]
        [TestCase(1234)]
        [TestCase(99999)]
        public void Run_With1000RandomDrops_NeverBreaksTheMergeInvariants(int seed)
        {
            var options = new SimulationOptions
            {
                Seed = seed,
                DropCooldown = COOLDOWN_SECONDS,
                SettleSeconds = SETTLE_SECONDS,
                RestartOnGameOver = true,
            };

            // The wall clock only measures how fast the harness is (issue #12: 1,000 drops in well under 60 s); the
            // simulation itself runs on its own clock.
            var stopwatch = Stopwatch.StartNew();

            using (var world = new SimulationWorld(options))
            {
                var merges = 0;
                var supernovas = 0;
                world.Merge.Merged += (tier, position, velocity) => merges++;
                world.Merge.SupernovaTriggered += position => supernovas++;

                var previousCount = 0;
                var previousDrops = 0;
                var previousMerges = 0;
                var previousSupernovas = 0;
                var previousRestarts = 0;
                var totalMerges = 0;

                /// <summary>Checks the piece count and the merged flags after one physics step.</summary>
                void CheckStep(SimulationWorld current)
                {
                    var count = current.CountBoardPieces();
                    var drops = current.PiecesDropped - previousDrops;
                    var stepMerges = merges - previousMerges;
                    var stepSupernovas = supernovas - previousSupernovas;
                    totalMerges += stepMerges;

                    // A restart clears the board between two steps: the first step of the new run has no baseline.
                    if (current.Restarts == previousRestarts)
                    {
                        var expected = previousCount + drops - stepMerges - 2 * stepSupernovas;
                        if (count != expected)
                        {
                            Assert.Fail($"Step {current.Steps}: expected {expected} pieces but found {count} ({previousCount} before + {drops} dropped - {stepMerges} merges - {stepSupernovas} supernovas).");
                        }
                    }

                    var pieces = current.Factory.ActivePieces;
                    for (var i = 0; i < pieces.Count; i++)
                    {
                        if (pieces[i].Merged)
                        {
                            Assert.Fail($"Step {current.Steps}: a merged piece (tier {pieces[i].Tier.Index}) is still on the board.");
                        }
                    }

                    previousCount = count;
                    previousDrops = current.PiecesDropped;
                    previousMerges = merges;
                    previousSupernovas = supernovas;
                    previousRestarts = current.Restarts;
                }

                var runner = new SimulationRunner(world, options);
                var result = runner.Play(DropScripts.Random(seed, DROP_COUNT), CheckStep);

                Assert.AreEqual(DROP_COUNT, result.PiecesDropped, "Every drop of the script was made.");
                Assert.GreaterOrEqual(totalMerges, MIN_EXPECTED_MERGES, "Too few merges happened for the invariants to mean anything.");
                Assert.Less(stopwatch.Elapsed.TotalSeconds, MAX_REAL_SECONDS, "Seconds the 1,000 drops took.");
            }
        }
    }
}
