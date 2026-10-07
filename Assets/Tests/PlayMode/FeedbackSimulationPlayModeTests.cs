using System.Collections.Generic;
using Coika.Core;
using NUnit.Framework;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the <see cref="Coika.Fx.FeedbackDirector"/> (issue #34) inside whole simulated runs: leaving it out must
    /// not change the physics or the score, and a run with it must play sounds and haptics with no warning.
    /// </summary>
    public class FeedbackSimulationPlayModeTests : HarnessTestBase
    {
        private const int SEED = 1234;
        private const int DROP_SEED = 77;
        private const int DROP_COUNT = 60;
        private const float SETTLE_SECONDS = 3f;

        /// <summary>
        /// The same seed and drops give the same result with and without the director, so the simulation result does
        /// not depend on the feedback, and the director really played something in the run that has it.
        /// </summary>
        [Test]
        public void Run_WithAndWithoutTheDirector_GivesTheIdenticalResult()
        {
            var drops = DropScripts.Random(DROP_SEED, DROP_COUNT);
            var onTrace = new List<string>();
            var offTrace = new List<string>();
            var played = 0;
            var vibrations = 0;

            var onOptions = NewOptions(true);
            SimulationResult on;
            using (var world = new SimulationWorld(onOptions))
            {
                on = new SimulationRunner(world, onOptions).Play(drops, w =>
                {
                    onTrace.Add(SimulationFingerprint.Of(w));
                    played = w.Audio.SfxCalls.Count;
                    vibrations = w.Haptics.Played.Count;
                });
            }

            var off = SimulationRunner.Run(NewOptions(false), drops, world => offTrace.Add(SimulationFingerprint.Of(world)));

            Assert.AreEqual(offTrace.Count, onTrace.Count, "The runs took a different number of steps.");
            for (var i = 0; i < onTrace.Count; i++)
            {
                if (onTrace[i] != offTrace[i])
                {
                    Assert.Fail($"The runs diverge at step {i}:\n  on:  {onTrace[i]}\n  off: {offTrace[i]}");
                }
            }

            Assert.AreEqual(off.ToString(), on.ToString());
            Assert.Greater(played, 0, "The director played no sound, so the comparison proved nothing.");
            Assert.Greater(vibrations, 0, "The director played no haptic.");
        }

        /// <summary>
        /// A long run with restarts plays merge sounds and one game-over sound for every run that ends, without a
        /// single warning (the base class fails the test on one).
        /// </summary>
        [Test]
        public void Run_WithRestarts_PlaysTheGameOverSoundOnEveryRun()
        {
            var options = NewOptions(true);
            options.RestartOnGameOver = true;

            using (var world = new SimulationWorld(options))
            {
                new SimulationRunner(world, options).Play(DropScripts.Random(DROP_SEED, 1000), null);

                var gameOvers = world.Audio.SfxCalls.FindAll(call => call.Id == SfxId.GameOver).Count;
                Assert.Greater(gameOvers, 0, "No run ended, so the check proves nothing.");
                Assert.AreEqual(world.Restarts + (world.IsGameOver ? 1 : 0), gameOvers, "One game-over sound per ended run.");
                Assert.Greater(world.Audio.SfxCalls.FindAll(call => call.Id == SfxId.Merge).Count, 0);
            }
        }

        /// <summary>
        /// The options of these runs: the golden seed, a few seconds to settle and the director on or off.
        /// </summary>
        /// <param name="feedback">Whether the director runs.</param>
        /// <returns>The options.</returns>
        private static SimulationOptions NewOptions(bool feedback)
        {
            return new SimulationOptions { Seed = SEED, SettleSeconds = SETTLE_SECONDS, Feedback = feedback };
        }
    }
}
