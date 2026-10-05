using System.Collections.Generic;
using Coika.Data;
using Coika.Fx;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the real <see cref="ParticleSpawner"/> of issue #32: counts, Reduce Shake, the quality multiplier, the
    /// hard caps and that bursting allocates nothing.
    /// </summary>
    public class ParticleSpawnerPlayModeTests : HarnessTestBase
    {
        private const int CAP = 40;
        private const int RING_CAP = 3;
        private const int BURSTS = 1000;

        private readonly List<Object> _created = new();
        private FeedbackConfig _config;
        private ParticleSpawner _spawner;

        /// <summary>
        /// Builds a small config with low caps and a spawner on it.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<FeedbackConfig>();
            _created.Add(_config);
            TestReflection.SetField(_config, "_maxLiveParticles", CAP);
            TestReflection.SetField(_config, "_maxLiveRings", RING_CAP);
            _spawner = TestParticleSpawner.Create(_created, _config);
        }

        /// <summary>
        /// Destroys everything the test created.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        /// <summary>
        /// A burst emits exactly the requested number of pixel particles and no ring.
        /// </summary>
        [Test]
        public void Burst_OfParticles_EmitsTheRequestedCount()
        {
            _spawner.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, 8);

            Assert.AreEqual(8, _spawner.LiveParticleCount);
            Assert.AreEqual(0, _spawner.LiveRingCount);
        }

        /// <summary>
        /// Each ring effect emits one ring and no particle, whatever the count asked.
        /// </summary>
        [Test]
        public void Burst_OfARingEffect_EmitsOneRing()
        {
            _spawner.Burst(FxKind.FlashRing, Vector2.zero, Color.white, 9);
            _spawner.Burst(FxKind.SupernovaFlash, Vector2.zero, Color.white, 9);

            Assert.AreEqual(2, _spawner.LiveRingCount);
            Assert.AreEqual(0, _spawner.LiveParticleCount);
        }

        /// <summary>
        /// Reduce Shake halves the particles but keeps the ring flash.
        /// </summary>
        [Test]
        public void Burst_WithReduceMotion_HalvesParticlesAndKeepsRing()
        {
            _spawner.ReduceMotion = true;

            _spawner.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, 10);
            _spawner.Burst(FxKind.FlashRing, Vector2.zero, Color.white, 1);

            Assert.AreEqual(5, _spawner.LiveParticleCount);
            Assert.AreEqual(1, _spawner.LiveRingCount);
        }

        /// <summary>
        /// A count multiplier of zero (lowest quality) emits no particles, and a small one still emits one.
        /// </summary>
        [Test]
        public void Burst_WithQualityMultiplier_ScalesTheCount()
        {
            _spawner.CountMultiplier = 0f;
            _spawner.Burst(FxKind.LandingDust, Vector2.zero, Color.white, 4);
            Assert.AreEqual(0, _spawner.LiveParticleCount);

            _spawner.CountMultiplier = 0.1f;
            _spawner.Burst(FxKind.LandingDust, Vector2.zero, Color.white, 4);
            Assert.AreEqual(1, _spawner.LiveParticleCount);
        }

        /// <summary>
        /// A chain of ten merges never has more live particles or rings than the caps.
        /// </summary>
        [Test]
        public void Burst_InAChainOfTenMerges_RespectsTheCaps()
        {
            for (var merge = 0; merge < 10; merge++)
            {
                _spawner.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, _config.MergeBurstMaxCount);
                _spawner.Burst(FxKind.FlashRing, Vector2.zero, Color.white, 1);

                Assert.LessOrEqual(_spawner.LiveParticleCount, CAP);
                Assert.LessOrEqual(_spawner.LiveRingCount, RING_CAP);
            }
        }

        /// <summary>
        /// Clearing the spawner removes every live particle.
        /// </summary>
        [Test]
        public void ResetAll_AfterBursts_LeavesNothingAlive()
        {
            _spawner.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, 8);
            _spawner.Burst(FxKind.Shockwave, Vector2.zero, Color.white, 1);

            _spawner.ResetAll();

            Assert.AreEqual(0, _spawner.LiveCount);
        }

        /// <summary>
        /// A thousand bursts of every effect allocate nothing.
        /// </summary>
        [Test]
        public void Burst_OfEveryEffect_AllocatesNothing()
        {
            _spawner.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, 8);

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < BURSTS; i++)
                {
                    _spawner.Burst((FxKind)(i % 6), new Vector2(i * 0.01f, 1f), Color.cyan, 8);
                }
            });

            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT);
        }
    }
}
