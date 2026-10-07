using System;
using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the pause and slow-mo hooks of the simulation harness (issue #40): a paused world does not advance, and
    /// the slow-mo of the director reaches a real time scale owner and goes back to normal speed.
    /// </summary>
    public class HarnessPausePlayModeTests : HarnessTestBase
    {
        private const int SEED = 1234;
        private const int DROP_SEED = 77;
        private const int DROP_COUNT = 6;
        private const int PAUSED_STEPS = 50;

        /// <summary>
        /// Steps requested while the run is paused leave the simulated time, the score and every piece exactly as they
        /// were, and the run moves again after the resume.
        /// </summary>
        [Test]
        public void Step_WhilePaused_LeavesTimeScoreAndPiecesUnchanged()
        {
            var options = new SimulationOptions { Seed = SEED };
            using (var world = new SimulationWorld(options))
            {
                new SimulationRunner(world, options).Play(DropScripts.Random(DROP_SEED, DROP_COUNT));
                var stepsBefore = world.Steps;
                var textBefore = world.Snapshot().ToString();

                world.Pause();
                for (var i = 0; i < PAUSED_STEPS; i++)
                {
                    world.Step();
                }

                Assert.IsTrue(world.IsPaused);
                Assert.AreEqual(stepsBefore, world.Steps, "Simulated time moved while paused.");
                Assert.AreEqual(textBefore, world.Snapshot().ToString(), "The board or the score changed while paused.");

                world.Resume();
                world.Step();

                Assert.IsFalse(world.IsPaused);
                Assert.AreEqual(stepsBefore + 1, world.Steps, "The run did not move after the resume.");
            }
        }

        /// <summary>
        /// A scripted pause after a drop really pauses the run, once per scripted drop, leaves it playing, and gives the
        /// result of the run without pauses.
        /// </summary>
        [Test]
        public void Play_WithAScriptedPause_PausesAtEachScriptedDropAndKeepsTheResult()
        {
            var drops = DropScripts.Random(DROP_SEED, DROP_COUNT);
            var plain = SimulationRunner.Run(new SimulationOptions { Seed = SEED }, drops);

            var scripted = new[] { 2, 4 };
            var options = new SimulationOptions { Seed = SEED, PauseAfterDrops = scripted, PauseSteps = PAUSED_STEPS };
            var pauses = 0;
            var resumes = 0;
            SimulationResult paused;
            using (var world = new SimulationWorld(options))
            {
                world.Manager.StateChanged += (previous, next) =>
                {
                    pauses += next == GameState.Paused ? 1 : 0;
                    resumes += previous == GameState.Paused && next == GameState.Playing ? 1 : 0;
                };
                paused = new SimulationRunner(world, options).Play(drops);
                Assert.IsFalse(world.IsPaused, "The run stayed paused.");
            }

            Assert.AreEqual(scripted.Length, pauses, "The run did not pause once per scripted drop.");
            Assert.AreEqual(scripted.Length, resumes, "Every pause should have been resumed.");
            Assert.AreEqual(plain.ToString(), paused.ToString());
        }

        /// <summary>
        /// Only a run in progress can be paused, and only a paused run can be resumed, so a misplaced call fails with a
        /// clear message instead of a warning in the log.
        /// </summary>
        [Test]
        public void PauseAndResume_InTheWrongState_Throw()
        {
            using (var world = new SimulationWorld(new SimulationOptions { Seed = SEED }))
            {
                world.StartRun();
                Assert.Throws<InvalidOperationException>(world.Resume, "A run in progress is not paused.");

                world.Manager.EndRun();
                Assert.Throws<InvalidOperationException>(world.Pause, "A finished run cannot be paused.");
            }
        }

        /// <summary>
        /// A merge that creates a heavy tier asks for a slow-mo: the real time scale owner writes the slow scale, and
        /// writes 1 again when the slow-mo has run its seconds.
        /// </summary>
        [Test]
        public void Merge_OfHeavyTier_SlowsTheTimeScaleAndRestoresIt()
        {
            var options = new SimulationOptions { Seed = SEED, SlowMo = true };
            using (var world = new SimulationWorld(options))
            {
                world.StartRun();
                MergeScenario.MergeTwo(world, HeavyTierMinusOne(world));

                Assert.IsTrue(world.TimeScaleOwner.IsSlowMo, "The merge did not start a slow-mo.");
                Assert.Less(world.TimeScale.Value, 1f);

                MergeScenario.RunOutSlowMo(world);

                Assert.AreEqual(1f, world.TimeScale.Value);
                Assert.AreEqual(1, world.ScreenFx.SlowMos.Count, "The request is also recorded by the fake effects.");
            }
        }

        /// <summary>
        /// Pausing during a slow-mo cancels it: the scale is 0 while paused and 1 after the resume.
        /// </summary>
        [Test]
        public void Pause_DuringASlowMo_CancelsItAndTheScaleIsOneAfterTheResume()
        {
            var options = new SimulationOptions { Seed = SEED, SlowMo = true };
            using (var world = new SimulationWorld(options))
            {
                world.StartRun();
                MergeScenario.MergeTwo(world, HeavyTierMinusOne(world));
                Assert.IsTrue(world.TimeScaleOwner.IsSlowMo);

                world.Pause();
                Assert.AreEqual(0f, world.TimeScale.Value);
                Assert.IsFalse(world.TimeScaleOwner.IsSlowMo);

                world.Resume();
                Assert.AreEqual(1f, world.TimeScale.Value);
            }
        }

        /// <summary>
        /// Gives the tier whose pair merges into the lowest heavy tier, the one that starts a slow-mo.
        /// </summary>
        /// <param name="world">The world, to read the config from.</param>
        /// <returns>The tier of the pair to merge.</returns>
        private static int HeavyTierMinusOne(SimulationWorld world)
        {
            return world.Config.Feedback.ScreenFx.HeavyMergeMinTier - 1;
        }
    }
}
