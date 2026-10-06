using System.Collections.Generic;
using NUnit.Framework;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// The allocation guard of issue #39: a long scripted run with every M2 system on (the piece animations, the merge
    /// ghosts, the pooled particles and the feedback director) must not allocate per frame while it plays, merges and
    /// restarts after a game over. The Profiler counter is the only valid measure in the Editor (see
    /// <see cref="AllocationMeter"/>), but the Editor itself makes sporadic allocations that are not the game's: with
    /// every game system off a window of 3600 steps still counts 25 to 400 of them, at random steps. A leak per
    /// frame makes one or more per step, so the guard compares the median window with a share of its steps, and the
    /// strict "0 per frame" is checked on a device with the Profiler (.planning/device-checklist-39.md).
    /// </summary>
    public class FullRunAllocationPlayModeTests : HarnessTestBase
    {
        private const int SEED = 1234;
        private const int WARM_UP_DROPS = 100;
        private const int WINDOW_DROPS = 60;
        private const int WINDOWS = 9;
        private const int FIRST_SCRIPT_SEED = 100;
        private const float SETTLE_SECONDS = 2f;

        /// <summary>The share of steps a window may allocate in, above the Editor noise floor of about 2 to 4 percent.</summary>
        private const double MAX_ALLOCATIONS_PER_STEP = 0.1;

        /// <summary>
        /// After a warm-up that fills every pool and cache, nine windows of 60 drops (with merges, bursts, sounds and
        /// restarts) are measured one by one. The median window must stay under a tenth of an allocation per step.
        /// </summary>
        [Test]
        public void Run_WithAllM2Systems_MedianWindowStaysUnderAllocationBudget()
        {
            var options = new SimulationOptions { Seed = SEED, SettleSeconds = SETTLE_SECONDS, RestartOnGameOver = true };
            var scripts = new List<IReadOnlyList<float>>();
            for (var i = 0; i < WINDOWS; i++)
            {
                scripts.Add(DropScripts.Random(FIRST_SCRIPT_SEED + i, WINDOW_DROPS));
            }

            var rates = new List<double>();
            using (var world = new SimulationWorld(options))
            {
                var runner = new SimulationRunner(world, options);
                runner.Play(DropScripts.Random(WARM_UP_DROPS, WARM_UP_DROPS));

                foreach (var script in scripts)
                {
                    var stepsBefore = world.Steps;
                    var droppedBefore = world.PiecesDropped;

                    var allocations = AllocationMeter.Measure(() => runner.Advance(script));

                    Assert.AreEqual(WINDOW_DROPS, world.PiecesDropped - droppedBefore, "Every measured drop should be made.");
                    rates.Add(allocations / (double)(world.Steps - stepsBefore));
                }
            }

            rates.Sort();
            var median = rates[rates.Count / 2];
            Assert.Less(median, MAX_ALLOCATIONS_PER_STEP,
                $"The median window made {median:0.000} allocations per step (all windows: {string.Join(", ", rates.ConvertAll(r => r.ToString("0.000")))}).");
        }
    }
}
