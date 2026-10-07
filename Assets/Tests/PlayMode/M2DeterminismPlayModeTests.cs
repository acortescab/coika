using System;
using System.Collections.Generic;
using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// The M2 feedback layer must never change a simulation (issue #40): for several seeds, a run with every M2 system on,
    /// with pauses inserted, or with slow-mo applied gives the identical result, step by step, as the run with every M2
    /// system off. A new variant or seed is one more entry in a table, not a new test.
    /// <para>
    /// Random drops never reach the tiers that start a slow-mo, so every run, the baseline too, gets the same pair of
    /// pieces of the last tier below the heavy one at the same simulated step: it merges into a heavy piece, which makes
    /// the director shake the screen and ask for a slow-mo.
    /// </para>
    /// </summary>
    public class M2DeterminismPlayModeTests : HarnessTestBase
    {
        private const int DROP_SEED = 77;
        private const int DROP_COUNT = 60;
        private const float SETTLE_SECONDS = 3f;
        private const long HEAVY_PAIR_STEP = 300;

        // Merges into tier 8, the lowest heavy tier of the GDD. The baseline world has no feedback config to read it from,
        // and the variants assert that the merge really shook the screen, so a change of the threshold fails them.
        private const int HEAVY_PAIR_TIER = 7;

        private static readonly int[] Seeds = { 1234, 1, 7, 99, 2026 };

        /// <summary>Each variant changes the options of the baseline run (all M2 systems off) the way its name says.</summary>
        private static readonly (string Name, Action<SimulationOptions> Apply)[] Variants =
        {
            ("AllM2On", options => EnableM2(options)),
            ("AllM2OnWithPauses", options =>
            {
                EnableM2(options);
                options.PauseAfterDrops = new[] { 3, 10, 11, 30, 45 };
                options.PauseSteps = 40;
            }),
            ("AllM2OnWithSlowMo", options =>
            {
                EnableM2(options);
                options.SlowMo = true;
            }),
            ("AllM2OnWithPausesAndSlowMo", options =>
            {
                EnableM2(options);
                options.SlowMo = true;
                options.PauseAfterDrops = new[] { 5, 20, 40 };
            }),
        };

        /// <summary>What the run with no M2 system gave for a seed, so each variant of the seed compares against it.</summary>
        private static readonly Dictionary<int, (List<string> Trace, SimulationResult Result)> Baselines = new();

        /// <summary>
        /// The cases of the test: every variant for every seed.
        /// </summary>
        /// <returns>One case per seed and variant.</returns>
        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (var seed in Seeds)
            {
                foreach (var variant in Variants)
                {
                    yield return new TestCaseData(seed, variant.Apply).SetName($"Run_{variant.Name}_GivesTheResultOfAllM2Off_Seed{seed}");
                }
            }
        }

        /// <summary>
        /// The variant gives the same per-step physics and the same final result as the run with no M2 system, and it
        /// really paused and slowed time down when it was asked to, so the comparison proves something.
        /// </summary>
        /// <param name="seed">Seed of the runs.</param>
        /// <param name="apply">Turns the baseline options into the options of the variant.</param>
        [TestCaseSource(nameof(Cases))]
        public void Run_WithAVariant_GivesTheResultOfAllM2Off(int seed, Action<SimulationOptions> apply)
        {
            var drops = DropScripts.Random(DROP_SEED + seed, DROP_COUNT);
            var baseline = BaselineOf(seed, drops);

            var options = NewOptions(seed);
            apply(options);

            var trace = new List<string>();
            var pauses = 0;
            SimulationResult result;
            using (var world = new SimulationWorld(options))
            {
                world.Manager.StateChanged += (_, next) => pauses += next == GameState.Paused ? 1 : 0;
                result = new SimulationRunner(world, options).Play(drops, w =>
                {
                    InjectHeavyPair(w);
                    trace.Add(SimulationFingerprint.Of(w));
                });

                Assert.IsFalse(world.IsPaused, "The run ended paused.");
                Assert.Greater(world.Audio.SfxCalls.Count, 0, "The director played no sound, so the comparison proved nothing.");
                Assert.Greater(world.ScreenFx.Shakes.Count, 0, "The heavy merge did not shake the screen.");
                if (options.PauseAfterDrops != null)
                {
                    Assert.Greater(pauses, 0, "The variant asked for pauses and none happened.");
                }

                if (options.SlowMo)
                {
                    Assert.Greater(world.TimeScale.Writes.Count, 0, "The slow-mo never reached the time scale.");
                    MergeScenario.RunOutSlowMo(world);
                    Assert.AreEqual(1f, world.TimeScale.Value, "The time scale was not back to normal.");
                }
            }

            CollectionAssert.AreEqual(baseline.Trace, trace, "The variant diverges from the run with no M2 system.");
            Assert.AreEqual(baseline.Result.ToString(), result.ToString());
        }

        /// <summary>
        /// Gives the run with no M2 system for a seed, playing it the first time it is asked for.
        /// </summary>
        /// <param name="seed">Seed of the run.</param>
        /// <param name="drops">The drop script of the seed.</param>
        /// <returns>The per-step fingerprints and the final result.</returns>
        private static (List<string> Trace, SimulationResult Result) BaselineOf(int seed, IReadOnlyList<float> drops)
        {
            if (!Baselines.TryGetValue(seed, out var baseline))
            {
                var trace = new List<string>();
                var result = SimulationRunner.Run(NewOptions(seed), drops, world =>
                {
                    InjectHeavyPair(world);
                    trace.Add(SimulationFingerprint.Of(world));
                });
                baseline = (trace, result);
                Baselines[seed] = baseline;
            }

            return baseline;
        }

        /// <summary>
        /// Puts the pair of pieces that merges into a heavy piece on the board, once, at a fixed simulated step.
        /// </summary>
        /// <param name="world">The world after a step.</param>
        private static void InjectHeavyPair(SimulationWorld world)
        {
            if (world.Steps == HEAVY_PAIR_STEP)
            {
                MergeScenario.PlacePair(world, HEAVY_PAIR_TIER);
            }
        }

        /// <summary>
        /// Turns every M2 system on, like the shipped game.
        /// </summary>
        /// <param name="options">The options to change.</param>
        private static void EnableM2(SimulationOptions options)
        {
            options.Animations = true;
            options.Particles = true;
            options.Feedback = true;
        }

        /// <summary>
        /// The baseline options: the seed, a few seconds to settle and every M2 system off.
        /// </summary>
        /// <param name="seed">Seed of the run.</param>
        /// <returns>The options.</returns>
        private static SimulationOptions NewOptions(int seed)
        {
            return new SimulationOptions
            {
                Seed = seed,
                SettleSeconds = SETTLE_SECONDS,
                Animations = false,
                Particles = false,
                Feedback = false,
            };
        }
    }
}
