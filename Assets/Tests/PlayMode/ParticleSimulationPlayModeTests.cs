using System.Collections.Generic;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the pooled particles (issue #32) inside whole simulated runs: they must never change the physics or the
    /// score, and a long run with every effect on must stay inside the caps with no warning (the harness base class
    /// fails the test on any warning, such as a pool that grew).
    /// </summary>
    public class ParticleSimulationPlayModeTests : HarnessTestBase
    {
        private const int SEED = 1234;
        private const int DROP_SEED = 77;
        private const int DROP_COUNT = 60;
        private const int LONG_DROP_COUNT = 1000;
        private const float SETTLE_SECONDS = 3f;
        // The last piece needs about a second to fall, and the longest particle (confetti) lives 1.2 s.
        private const float SETTLE_AFTER_LAST_DROP = 4f;
        private const int CHAIN_PARTICLE_CAP = 12;
        private const int CHAIN_RING_CAP = 2;

        /// <summary>
        /// The same seed and drops give the same exact positions at every step and the same result with the
        /// particles on and off.
        /// </summary>
        [Test]
        public void Run_WithParticlesOnAndOff_GivesIdenticalPhysicsAndScore()
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

            Assert.Greater(on.Merges, 0, "No merge happened, so the particles proved nothing.");
            Assert.AreEqual(off.ToString(), on.ToString());
        }

        /// <summary>
        /// A thousand drops with restarts never push the live particles or rings over the caps, emit particles, and
        /// leave nothing alive once the settle time has passed. The run ends with no warning.
        /// </summary>
        [Test]
        public void Run_WithAThousandDrops_StaysInsideTheCapsAndLeavesNothingAlive()
        {
            var options = NewOptions(true);
            options.RestartOnGameOver = true;
            options.SettleSeconds = SETTLE_AFTER_LAST_DROP;
            var maxParticles = 0;
            var maxRings = 0;

            using (var world = new SimulationWorld(options))
            {
                var feedback = world.Config.Feedback;
                new SimulationRunner(world, options).Play(DropScripts.Random(DROP_SEED, LONG_DROP_COUNT), w =>
                {
                    maxParticles = Mathf.Max(maxParticles, w.Particles.LiveParticleCount);
                    maxRings = Mathf.Max(maxRings, w.Particles.LiveRingCount);
                });

                Assert.Greater(maxParticles, 0, "No particle was ever emitted, so the check proves nothing.");
                Assert.LessOrEqual(maxParticles, feedback.Particles.MaxLiveParticles);
                Assert.LessOrEqual(maxRings, feedback.Particles.MaxLiveRings);
                Assert.AreEqual(0, world.Particles.LiveCount, "Every particle ages out once the run settles.");
            }
        }

        /// <summary>
        /// A chain of ten merges, with the caps set low enough to be reached, fills the particle cap and never goes
        /// over them, through the real director and spawner.
        /// </summary>
        [Test]
        public void Run_WithAChainOfTenMerges_FillsTheParticleCapAndNeverExceedsTheCaps()
        {
            var options = NewOptions(true);
            options.ParticleCap = CHAIN_PARTICLE_CAP;
            options.RingCap = CHAIN_RING_CAP;

            using (var world = new SimulationWorld(options))
            {
                world.StartRun();
                var merges = world.Tiers.Count - 1;
                for (var tier = 0; tier < merges; tier++)
                {
                    MergeScenario.MergeTwo(world, tier);

                    Assert.LessOrEqual(world.Particles.LiveParticleCount, CHAIN_PARTICLE_CAP, $"Particles after the merge of tier {tier}.");
                    Assert.LessOrEqual(world.Particles.LiveRingCount, CHAIN_RING_CAP, $"Rings after the merge of tier {tier}.");
                }

                Assert.AreEqual(CHAIN_PARTICLE_CAP, world.Particles.LiveParticleCount, "The particle cap is reached.");
                Assert.Greater(world.Particles.LiveRingCount, 0, "The flash rings of the last merges are still showing.");
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
        /// The options of these runs: the golden seed, a few seconds to settle and the particles on or off.
        /// </summary>
        /// <param name="particles">Whether the particles run.</param>
        /// <returns>The options.</returns>
        private static SimulationOptions NewOptions(bool particles)
        {
            return new SimulationOptions { Seed = SEED, SettleSeconds = SETTLE_SECONDS, Particles = particles };
        }
    }
}
