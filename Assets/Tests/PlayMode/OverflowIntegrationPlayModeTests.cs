using System.Collections.Generic;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// The overflow rule of the GDD (§3.6) through the whole game, driven by real drops: a column that settles above
    /// the Danger Line ends the game 2 s later, and pieces that only cross the line while they fall do not. The
    /// first test uses a low jar (10 x 6) so four pieces reach the line, and the harness clock, so 2 s is measured
    /// in simulated time. It uses the asset service double.
    /// </summary>
    public class OverflowIntegrationPlayModeTests : HarnessTestBase
    {
        private const float GAME_OVER_SECONDS = 2f;
        private const float GAME_OVER_TOLERANCE = 0.12f;
        private const float LOW_JAR_HEIGHT = 6f;
        private const float JAR_WIDTH = 10f;
        private const float DROP_SPACING_SECONDS = 1.5f;
        private const float WAIT_FOR_GAME_OVER_SECONDS = 10f;
        private const float OVERFLOW_GRACE_SECONDS = 1f;
        private const float MAX_PROGRESS_WHILE_FALLING = 0.5f;

        /// <summary>
        /// Four pieces of alternating tiers dropped in one column rise above the Danger Line of the low jar and settle
        /// there: the game ends 2.0 s after the first moment a settled piece is above the line (0.1 s of tolerance
        /// plus one physics step, because the detector looks every 0.1 s).
        /// </summary>
        [Test]
        public void Run_WithAColumnSettledAboveTheDangerLine_EndsTheGameAfterTwoSeconds()
        {
            var options = new SimulationOptions
            {
                Seed = 1,
                JarSize = new Vector2(JAR_WIDTH, LOW_JAR_HEIGHT),
                ForcedOpening = new[] { 4, 3, 4, 3, 4, 3 },
                MinSecondsBetweenDrops = DROP_SPACING_SECONDS,
                SettleSeconds = WAIT_FOR_GAME_OVER_SECONDS,
            };

            using (var world = new SimulationWorld(options))
            {
                var overflowingSince = -1.0;
                var gameOverAt = -1.0;

                /// <summary>Tracks the time of the continuous overflow and of the game over.</summary>
                void Watch(SimulationWorld current)
                {
                    if (IsAnyPieceOverflowing(current))
                    {
                        if (overflowingSince < 0.0)
                        {
                            overflowingSince = current.SimulatedSeconds;
                        }
                    }
                    else
                    {
                        overflowingSince = -1.0;
                    }

                    if (current.IsGameOver && gameOverAt < 0.0)
                    {
                        gameOverAt = current.SimulatedSeconds;
                    }
                }

                var result = new SimulationRunner(world, options).Play(DropScripts.Column(0f, 4), Watch);

                Assert.IsTrue(result.GameOver, "The column should have ended the game.");
                Assert.GreaterOrEqual(overflowingSince, 0.0, "A settled piece should have been above the line.");
                Assert.AreEqual(GAME_OVER_SECONDS, gameOverAt - overflowingSince, GAME_OVER_TOLERANCE, "Seconds between the piece settling above the line and the game over.");
            }
        }

        /// <summary>
        /// Twelve drops spread across the jar start above the Danger Line, because the Drop Line is above it, and fall
        /// through the zone: the detector must not count that, so the game goes on and the timer never gets close to
        /// the end. The test also checks that pieces really were above the line while moving.
        /// </summary>
        [Test]
        public void Run_WithPiecesCrossingTheLineWhileFalling_DoesNotEndTheGame()
        {
            var options = new SimulationOptions
            {
                Seed = 3,
                MinSecondsBetweenDrops = OVERFLOW_GRACE_SECONDS + 0.2f,
                SettleSeconds = 3f,
            };
            var drops = new List<float> { -4f, -2f, 0f, 2f, 4f, -3f, -1f, 1f, 3f, -4f, 0f, 4f };

            using (var world = new SimulationWorld(options))
            {
                var crossedWhileMoving = false;
                var worstProgress = 0f;

                /// <summary>Tracks the overflow progress and whether a piece was above the line while moving.</summary>
                void Watch(SimulationWorld current)
                {
                    worstProgress = Mathf.Max(worstProgress, current.Overflow.WorstOverflowProgress);
                    var pieces = current.Factory.ActivePieces;
                    for (var i = 0; i < pieces.Count; i++)
                    {
                        var piece = pieces[i];
                        if (!piece.IsHeld && !piece.IsSettled && piece.Collider.bounds.max.y > current.Jar.DangerLineY)
                        {
                            crossedWhileMoving = true;
                        }
                    }
                }

                var result = new SimulationRunner(world, options).Play(drops, Watch);

                Assert.IsTrue(crossedWhileMoving, "No piece was above the line while moving, so the test proves nothing.");
                Assert.IsFalse(result.GameOver, "Pieces that only cross the line while moving must not end the game.");
                Assert.Less(worstProgress, MAX_PROGRESS_WHILE_FALLING, "Worst overflow progress seen.");
                Assert.AreEqual(drops.Count, result.PiecesDropped);
            }
        }

        /// <summary>
        /// Whether a piece counts for the overflow timer right now: not held, top above the Danger Line, settled, older
        /// than the grace and past the grace of a merge. It repeats the rule of the detector on the clock of the world.
        /// </summary>
        /// <param name="world">The world to look at.</param>
        /// <returns>True when at least one piece would accrue overflow time.</returns>
        private static bool IsAnyPieceOverflowing(SimulationWorld world)
        {
            var now = world.Now;
            var pieces = world.Factory.ActivePieces;
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece.IsHeld)
                {
                    continue;
                }

                var past = now - piece.SpawnTime > world.Config.OverflowGrace && !piece.IsInSpawnGraceAt(now);
                if (past && piece.IsSettled && piece.Collider.bounds.max.y > world.Jar.DangerLineY)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
