using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Determinism of the whole game (GDD §19, S-63), checked through the simulation harness: the same seed and the
    /// same drop X positions give exactly the same result, in the same session and, through a golden file, across
    /// domain reloads and Editor sessions (the golden file is the baseline that outlives a reload, so the reload
    /// check does not need a test of its own); a different seed gives a different one. Each run builds a fresh world
    /// from the asset service double, so no bundles are needed.
    /// </summary>
    public class SimulationDeterminismPlayModeTests : HarnessTestBase
    {
        private const string GOLDEN_RELATIVE_PATH = "Tests/PlayMode/Golden/simulation-seed-1234.txt";
        private const string UPDATE_GOLDEN_VARIABLE = "COIKA_UPDATE_GOLDEN";
        private const int SEED = 1234;
        private const int DROP_SEED = 77;
        private const int DROP_COUNT = 60;
        private const float SETTLE_SECONDS = 3f;

        /// <summary>
        /// Two runs with the same seed and the same drops end with the same score, tiers, merges, time and pieces at
        /// the same rounded positions, compared as text byte for byte.
        /// </summary>
        [Test]
        public void Run_WithTheSameSeedAndDrops_GivesAnIdenticalResult()
        {
            var drops = DropScripts.Random(DROP_SEED, DROP_COUNT);

            var first = SimulationRunner.Run(NewOptions(SEED), drops);
            var second = SimulationRunner.Run(NewOptions(SEED), drops);

            Assert.Greater(first.PiecesDropped, 0, "The run did nothing, so equality would prove nothing.");
            Assert.Greater(first.Pieces.Count, 0, "The board is empty, so the piece list proves nothing.");
            Assert.AreEqual(first.ToString(), second.ToString());
        }

        /// <summary>
        /// A different seed gives a different result with the same drops, so the harness does not ignore the seed.
        /// </summary>
        [Test]
        public void Run_WithADifferentSeed_GivesADifferentResult()
        {
            var drops = DropScripts.Random(DROP_SEED, DROP_COUNT);

            var first = SimulationRunner.Run(NewOptions(SEED), drops);
            var other = SimulationRunner.Run(NewOptions(SEED + 1), drops);

            Assert.AreNotEqual(first.OutcomeText, other.OutcomeText, "Everything but the seed is the same, so the harness ignores the seed.");
        }

        /// <summary>
        /// The result of the fixed seed and drops equals the one stored in the repository. The file is the baseline that
        /// survives a domain reload, an Editor restart and a new checkout. When it is missing, or when the variable
        /// <c>COIKA_UPDATE_GOLDEN</c> is set to 1 (after an intended change of the rules or of the Unity or physics
        /// version), the test writes it and ends inconclusive: commit the file and run again.
        /// </summary>
        [Test]
        public void Run_WithTheGoldenSeedAndDrops_MatchesTheGoldenFile()
        {
            var path = Path.Combine(Application.dataPath, GOLDEN_RELATIVE_PATH);
            var result = SimulationRunner.Run(NewOptions(SEED), DropScripts.Random(DROP_SEED, DROP_COUNT));

            if (!File.Exists(path) || Environment.GetEnvironmentVariable(UPDATE_GOLDEN_VARIABLE) == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, result + "\n");
                Assert.Inconclusive($"The golden file was written to {path}. Commit it and run the test again.");
            }

            var expected = File.ReadAllText(path).TrimEnd('\r', '\n').Replace("\r\n", "\n");
            Assert.AreEqual(expected, result.ToString(), "The simulation no longer matches the golden file. If the change is intended, regenerate it with COIKA_UPDATE_GOLDEN=1.");
        }

        /// <summary>
        /// The options of the determinism runs: the shipped rules, a fixed seed and a few seconds to let the board settle.
        /// </summary>
        /// <param name="seed">Seed of the run.</param>
        /// <returns>The options.</returns>
        private static SimulationOptions NewOptions(int seed)
        {
            return new SimulationOptions { Seed = seed, SettleSeconds = SETTLE_SECONDS };
        }
    }
}
