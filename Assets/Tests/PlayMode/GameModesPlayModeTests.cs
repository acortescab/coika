using System.Collections.Generic;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// The game modes through the simulation harness (issue #66): a Daily retry replays the same board, a Classic
    /// retry draws a new one, and a mode that does not end on overflow keeps playing where Classic loses. It uses the
    /// asset service double and the harness clock.
    /// </summary>
    public class GameModesPlayModeTests : HarnessTestBase
    {
        private const int OPENING_TIERS = 8;
        private const float LOW_JAR_HEIGHT = 6f;
        private const float JAR_WIDTH = 10f;
        private const float DROP_SPACING_SECONDS = 1.5f;
        private const float WAIT_SECONDS = 10f;
        private const int DAILY_SEED = 20261007;

        /// <summary>
        /// Retrying a Daily run starts a run with the same seed and the same upcoming pieces, even after the clock
        /// moved to the next UTC day.
        /// </summary>
        [Test]
        public void Retry_InDaily_ReplaysTheIdenticalPieceSequence()
        {
            var options = new SimulationOptions { Seed = 1, Mode = GameMode.Daily };

            using (var world = new SimulationWorld(options))
            {
                var runs = new List<int[]>();
                world.Manager.RunStarted += context => runs.Add(Opening(world.Config, context.Queue));

                world.StartRun();
                world.Manager.EndRun();
                world.Clock.UtcNow = world.Clock.UtcNow.AddDays(1);
                world.Restart();

                Assert.AreEqual(2, runs.Count);
                Assert.AreEqual(runs[0], runs[1], "A Daily retry should deal the same pieces.");
                Assert.AreEqual(DAILY_SEED, world.RunSetup.Seed, "The seed is the date of the first run, not of the retry.");
            }
        }

        /// <summary>
        /// Retrying a Classic run draws a new seed, so the board is not the same.
        /// </summary>
        [Test]
        public void Retry_InClassic_DealsANewSequence()
        {
            var options = new SimulationOptions { Seed = 1, Mode = GameMode.Classic };

            using (var world = new SimulationWorld(options))
            {
                var seeds = new List<int>();
                world.Manager.RunStarted += context => seeds.Add(context.Queue.Seed);

                world.StartRun();
                world.Manager.EndRun();
                world.Restart();

                Assert.AreEqual(new[] { 1, 2 }, seeds.ToArray());
            }
        }

        /// <summary>
        /// The column that ends a Classic run in the low jar does not end a Zen run: the detector is never enabled.
        /// </summary>
        [Test]
        public void Run_WithAColumnAboveTheDangerLine_EndsInClassicButNotInZen()
        {
            Assert.IsTrue(PlayColumn(GameMode.Classic), "The control run in Classic should end.");
            Assert.IsFalse(PlayColumn(GameMode.Zen), "A Zen run should keep playing.");
        }

        /// <summary>
        /// Plays four pieces in one column of a low jar and tells whether the run ended.
        /// </summary>
        /// <param name="mode">Mode of the run.</param>
        /// <returns>True when the game ended.</returns>
        private static bool PlayColumn(GameMode mode)
        {
            var options = new SimulationOptions
            {
                Seed = 1,
                Mode = mode,
                JarSize = new Vector2(JAR_WIDTH, LOW_JAR_HEIGHT),
                ForcedOpening = new[] { 4, 3, 4, 3, 4, 3 },
                MinSecondsBetweenDrops = DROP_SPACING_SECONDS,
                SettleSeconds = WAIT_SECONDS,
            };

            using (var world = new SimulationWorld(options))
            {
                return new SimulationRunner(world, options).Play(DropScripts.Column(0f, 4)).GameOver;
            }
        }

        /// <summary>
        /// Reads the first tiers a run deals: a copy of the queue built from the same seed is advanced, so the queue
        /// of the run is left untouched.
        /// </summary>
        private static int[] Opening(GameConfig config, SpawnQueue queue)
        {
            var copy = new SpawnQueue(config, queue.Seed);
            var tiers = new int[OPENING_TIERS];
            for (var i = 0; i < tiers.Length; i++)
            {
                tiers[i] = copy.Advance();
            }

            return tiers;
        }
    }
}
