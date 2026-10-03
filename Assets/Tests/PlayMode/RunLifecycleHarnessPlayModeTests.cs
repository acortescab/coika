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

        /// <summary>
        /// The state at the start of runs 2 and 3 equals the state at the start of run 1, and every run had pieces on
        /// the board before it ended, so the comparison is not an empty one.
        /// </summary>
        [Test]
        public void Retry_ThreeConsecutiveRuns_StartEachRunFromTheSameState()
        {
            var options = new SimulationOptions { Seed = 11, SettleSeconds = 1f };

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
                }
            }
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
            builder.Append(" piecesInWorld=").Append(Object.FindObjectsByType<Piece>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            builder.Append(" drop=").Append(world.Controller.State).Append('/').Append(world.Controller.IsEnabled);
            builder.Append(" overflowRunning=").Append(world.Overflow.IsRunning);
            builder.Append(" mergeEnabled=").Append(world.Merge.enabled);
            builder.Append(" physics=").Append(Physics2D.simulationMode);

            builder.Append(" input=").Append(EventListeners.Count(world.Input, "DropPressed")).Append('/').Append(EventListeners.Count(world.Input, "DropReleased"));
            builder.Append(" merge=").Append(EventListeners.Count(world.Merge, "Merged")).Append('/').Append(EventListeners.Count(world.Merge, "SupernovaTriggered"));
            builder.Append(" overflow=").Append(EventListeners.Count(world.Overflow, "GameOverTriggered")).Append('/').Append(EventListeners.Count(world.Overflow, "OverflowProgressChanged"));
            builder.Append(" controller=").Append(EventListeners.Count(world.Controller, "PieceDropped")).Append('/').Append(EventListeners.Count(world.Controller, "StateChanged"));
            builder.Append(" score=").Append(EventListeners.Count(world.Score, "ScoreChanged")).Append('/').Append(EventListeners.Count(world.Score, "NewBestReached"));
            builder.Append(" combo=").Append(EventListeners.Count(world.Score.ComboTracker, "ComboChanged"));
            builder.Append(" manager=").Append(EventListeners.Count(world.Manager, "StateChanged"))
                .Append('/').Append(EventListeners.Count(world.Manager, "RunStarted"))
                .Append('/').Append(EventListeners.Count(world.Manager, "RunEnded"))
                .Append('/').Append(EventListeners.Count(world.Manager, "GameOverReady"));
            builder.Append(" factory=").Append(EventListeners.Count(world.Factory, "PieceCreated"));

            var collided = 0;
            for (var i = 0; i < world.Factory.ActivePieces.Count; i++)
            {
                collided += EventListeners.Count(world.Factory.ActivePieces[i], "Collided");
            }

            builder.Append(" collided=").Append(collided);
            return builder.ToString();
        }
    }
}
