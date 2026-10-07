using System.Text;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Regression test of the run lifecycle (issue #11, extended in #12): three consecutive runs through
    /// <see cref="GameManager.Retry"/>, each played with real drops, must start from exactly the same state as the
    /// first: the same objects in the world, the same number of subscribers on every event, the same manager state.
    /// It complements the Game scene version in <c>GameLoopPlayModeTests</c> by using real drops and merges instead of
    /// a filled board, and the asset service double.
    /// </summary>
    public class RunLifecycleHarnessPlayModeTests : HarnessTestBase
    {
        private const int DROPS_PER_RUN = 12;
        private const int RUN_COUNT = 3;
        private const int DROP_SEED = 5;
        private const float DRAIN_SECONDS = 5f;

        /// <summary>
        /// The state at the start of runs 2 and 3 equals the state at the start of run 1, and every run had pieces on
        /// the board before it ended, so the comparison is not an empty one.
        /// </summary>
        /// <param name="seed">Seed of the first run.</param>
        [TestCase(11)]
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(99)]
        [TestCase(2026)]
        public void Retry_ThreeConsecutiveRuns_StartEachRunFromTheSameState(int seed)
        {
            var options = new SimulationOptions { Seed = seed, SettleSeconds = 1f, SlowMo = true };

            using (var world = new SimulationWorld(options))
            {
                var runner = new SimulationRunner(world, options);
                world.StartRun();
                var baseline = Describe(world);
                Assert.IsTrue(baseline.Contains("state=Playing"), baseline);

                for (var run = 0; run < RUN_COUNT; run++)
                {
                    runner.Continue(DropScripts.Random(DROP_SEED + run, DROPS_PER_RUN));
                    Assert.Greater(world.CountBoardPieces(), 0, $"Run {run + 1} left nothing on the board.");

                    if (!world.IsGameOver)
                    {
                        world.Manager.EndRun();
                    }

                    Assert.AreEqual(GameState.GameOver, world.Manager.State);
                    world.Restart();

                    Assert.AreEqual(baseline, Describe(world), $"Run {run + 2} did not start like run 1.");
                    AssertDrains(world);
                }
            }
        }

        /// <summary>
        /// Pausing and resuming in the middle of a run leaves the pools, the subscribers and the time scale as they were
        /// before the pause, and the next Retry still starts from the state of the first run.
        /// </summary>
        /// <param name="seed">Seed of the run.</param>
        [TestCase(11)]
        [TestCase(1)]
        [TestCase(7)]
        [TestCase(99)]
        [TestCase(2026)]
        public void PauseAndResume_InTheMiddleOfARun_LeaveNothingBehind(int seed)
        {
            var options = new SimulationOptions { Seed = seed, SettleSeconds = 1f, SlowMo = true };

            using (var world = new SimulationWorld(options))
            {
                var runner = new SimulationRunner(world, options);
                world.StartRun();
                var baseline = Describe(world);

                runner.Continue(DropScripts.Random(DROP_SEED, DROPS_PER_RUN));
                var beforePause = Describe(world);
                Assert.AreEqual(GameState.Playing, world.Manager.State, "The run ended before it could be paused.");

                world.Pause();
                Assert.AreEqual(0f, world.TimeScale.Value, "Pausing sets the time scale to 0.");
                world.Resume();

                Assert.AreEqual(beforePause, Describe(world), "The pause left something behind.");

                if (!world.IsGameOver)
                {
                    world.Manager.EndRun();
                }

                world.Restart();
                Assert.AreEqual(baseline, Describe(world), "The run after the pause did not start like run 1.");
                AssertDrains(world);
            }
        }

        /// <summary>
        /// Steps the world with no drops until every effect of the last run has played out, then checks that no particle
        /// or merge ghost is left alive.
        /// </summary>
        /// <param name="world">The world, in a started run.</param>
        private static void AssertDrains(SimulationWorld world)
        {
            var steps = Mathf.CeilToInt(DRAIN_SECONDS / world.FixedDeltaTime);
            for (var i = 0; i < steps; i++)
            {
                world.Step();
            }

            Assert.AreEqual(0, world.Particles.LiveCount, "Particles stayed alive.");
            Assert.AreEqual(0, world.Ghosts.ActiveCount, "Merge ghosts stayed alive.");
        }

        /// <summary>
        /// Describes everything a run could leave behind: the pieces in the world, the pool, every event's subscribers,
        /// the manager state, the score and the combo.
        /// </summary>
        /// <param name="world">The world to describe.</param>
        /// <returns>A text that is equal at the start of every run when nothing leaks.</returns>
        private static string Describe(SimulationWorld world)
        {
            var builder = new StringBuilder();
            builder.Append("state=").Append(world.Manager.State);
            builder.Append(" score=").Append(world.Score.Score);
            builder.Append(" multiplier=").Append(world.Score.ComboTracker.Multiplier);
            builder.Append(" activePieces=").Append(world.Factory.ActivePieces.Count);
            builder.Append(" piecesInWorld=").Append(Object.FindObjectsByType<Piece>(FindObjectsInactive.Include).Length);
            builder.Append(" drop=").Append(world.Controller.State).Append('/').Append(world.Controller.IsEnabled);
            builder.Append(" overflowRunning=").Append(world.Overflow.IsRunning);
            builder.Append(" mergeEnabled=").Append(world.Merge.enabled);
            builder.Append(" physics=").Append(Physics2D.simulationMode);

            builder.Append(" input=").Append(EventListeners.Count(world.Input, "DropPressed")).Append('/').Append(EventListeners.Count(world.Input, "DropReleased"));
            builder.Append(" merge=").Append(EventListeners.Count(world.Merge, "Merged")).Append('/').Append(EventListeners.Count(world.Merge, "SupernovaTriggered"));
            builder.Append(" overflow=").Append(EventListeners.Count(world.Overflow, "GameOverTriggered")).Append('/').Append(EventListeners.Count(world.Overflow, "OverflowProgressChanged")).Append('/').Append(EventListeners.Count(world.Overflow, "DangerChanged"));
            builder.Append(" controller=").Append(EventListeners.Count(world.Controller, "PieceDropped")).Append('/').Append(EventListeners.Count(world.Controller, "StateChanged")).Append('/').Append(EventListeners.Count(world.Controller, "PieceSpawned"));
            builder.Append(" score=").Append(EventListeners.Count(world.Score, "ScoreChanged")).Append('/').Append(EventListeners.Count(world.Score, "NewBestReached"));
            builder.Append(" combo=").Append(EventListeners.Count(world.Score.ComboTracker, "ComboChanged"));
            builder.Append(" manager=").Append(EventListeners.Count(world.Manager, "StateChanged"))
                .Append('/').Append(EventListeners.Count(world.Manager, "RunStarted"))
                .Append('/').Append(EventListeners.Count(world.Manager, "RunEnded"))
                .Append('/').Append(EventListeners.Count(world.Manager, "GameOverReady"));
            builder.Append(" factory=").Append(EventListeners.Count(world.Factory, "PieceCreated"));

            var collided = 0;
            var landed = 0;
            for (var i = 0; i < world.Factory.ActivePieces.Count; i++)
            {
                collided += EventListeners.Count(world.Factory.ActivePieces[i], "Collided");
                landed += EventListeners.Count(world.Factory.ActivePieces[i], "Landed");
            }

            builder.Append(" collided=").Append(collided).Append(" landed=").Append(landed);

            // The live particles and ghosts of the last run play out their lifetime in the next one, so only the size of the
            // pools is compared here; AssertDrains checks that nothing stays alive.
            var systems = world.Particles.GetComponentsInChildren<ParticleSystem>(true);
            var capacity = 0;
            for (var i = 0; i < systems.Length; i++)
            {
                capacity += systems[i].main.maxParticles;
            }

            builder.Append(" particleSystems=").Append(systems.Length).Append('/').Append(capacity)
                .Append(" timeScale=").Append(world.TimeScale.Value).Append('/').Append(world.TimeScaleOwner.IsSlowMo);
            return builder.ToString();
        }
    }
}
