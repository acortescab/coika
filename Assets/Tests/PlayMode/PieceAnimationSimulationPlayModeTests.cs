using System.Collections.Generic;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the visual piece animations (issue #31) inside whole simulated runs: they must never change the
    /// physics, chains of merges must never leave a stuck scale or a lingering ghost, and the golden simulation of
    /// issue #12 must be the same with the animations running.
    /// </summary>
    public class PieceAnimationSimulationPlayModeTests : HarnessTestBase
    {
        private const int SEED = 1234;
        private const int DROP_SEED = 77;
        private const int DROP_COUNT = 60;
        private const int LONG_DROP_COUNT = 1000;
        private const float SETTLE_SECONDS = 3f;
        // The last piece needs about a second to fall from the Drop Line, then its landing and pops to end.
        private const float SETTLE_AFTER_LAST_DROP = 4f;

        /// <summary>
        /// The same seed and drops give the same result, and the same exact positions at every step, with the
        /// animations on and off.
        /// </summary>
        [Test]
        public void Run_WithAnimationsOnAndOff_GivesIdenticalPhysics()
        {
            var drops = DropScripts.Random(DROP_SEED, DROP_COUNT);
            var onTrace = new List<string>();
            var offTrace = new List<string>();

            var on = SimulationRunner.Run(NewOptions(true), drops, world => onTrace.Add(Fingerprint(world)));
            var off = SimulationRunner.Run(NewOptions(false), drops, world => offTrace.Add(Fingerprint(world)));

            Assert.AreEqual(offTrace.Count, onTrace.Count, "The runs took a different number of steps.");
            for (var i = 0; i < onTrace.Count; i++)
            {
                if (onTrace[i] != offTrace[i])
                {
                    Assert.Fail($"The runs diverge at step {i}:\n  on:  {onTrace[i]}\n  off: {offTrace[i]}");
                }
            }

            Assert.Greater(on.Merges, 0, "No merge happened, so the merge animations proved nothing.");
            Assert.AreEqual(off.ToString(), on.ToString());
        }

        /// <summary>
        /// A merge starts one ghost per source piece, and every ghost has shrunk and hidden once the shrink time has
        /// passed.
        /// </summary>
        [Test]
        public void Merge_OfTwoPieces_ShowsTwoGhostsThatThenDisappear()
        {
            var options = NewOptions(true);
            options.ForcedOpening = new[] { 0, 0, 0 };
            options.SettleSeconds = SETTLE_AFTER_LAST_DROP;
            var maxGhosts = 0;

            using (var world = new SimulationWorld(options))
            {
                new SimulationRunner(world, options).Play(new[] { 0f, 0f }, w => maxGhosts = Mathf.Max(maxGhosts, w.Ghosts.ActiveCount));

                Assert.GreaterOrEqual(world.Score.Merges, 1, "The two pieces should have merged.");
                Assert.GreaterOrEqual(maxGhosts, 2, "One ghost per source piece.");
                Assert.AreEqual(0, world.Ghosts.ActiveCount, "Every ghost is gone after the shrink time.");
            }
        }

        /// <summary>
        /// After a thousand drops with restarts, no piece animates, every pooled piece (in play or in the pool) is at
        /// scale one and no ghost is left.
        /// </summary>
        [Test]
        public void Run_WithAThousandDrops_LeavesEveryPooledPieceAtScaleOne()
        {
            var options = NewOptions(true);
            options.RestartOnGameOver = true;
            options.SettleSeconds = SETTLE_AFTER_LAST_DROP;

            using (var world = new SimulationWorld(options))
            {
                new SimulationRunner(world, options).Play(DropScripts.Random(DROP_SEED, LONG_DROP_COUNT));

                Assert.AreEqual(0, world.CountPiecesAnimating(), "No piece animates after the settle time.");
                Assert.AreEqual(0, world.Ghosts.ActiveCount, "No ghost is left.");

                var animators = world.Container.GetComponentsInChildren<PieceAnimator>(true);
                Assert.Greater(animators.Length, 0, "The prefab has no animators, so the check proves nothing.");
                foreach (var animator in animators)
                {
                    Assert.IsFalse(animator.IsRunning, "An animator is still running.");
                    Assert.AreEqual(Vector3.one, animator.VisualScale, "A pooled piece has a stuck scale.");
                }
            }
        }

        /// <summary>
        /// Describes the world after a step: the score, the pieces on the board and the sum of their exact positions
        /// and rotations.
        /// </summary>
        /// <param name="world">The world after a step.</param>
        /// <returns>A text that is equal in two runs exactly when their physics are in the same state.</returns>
        private static string Fingerprint(SimulationWorld world)
        {
            var x = 0f;
            var y = 0f;
            var angle = 0f;
            var pieces = world.Factory.ActivePieces;
            for (var i = 0; i < pieces.Count; i++)
            {
                var body = pieces[i].Rigidbody;
                x += body.position.x;
                y += body.position.y;
                angle += body.rotation;
            }

            return $"step={world.Steps} score={world.Score.Score} board={world.CountBoardPieces()} sumX={x:R} sumY={y:R} sumAngle={angle:R}";
        }

        /// <summary>
        /// The options of these runs: the golden seed, a few seconds to settle and the animations on or off.
        /// </summary>
        /// <param name="animations">Whether the visual animations run.</param>
        /// <returns>The options.</returns>
        private static SimulationOptions NewOptions(bool animations)
        {
            return new SimulationOptions { Seed = SEED, SettleSeconds = SETTLE_SECONDS, Animations = animations };
        }
    }
}
