using System.Collections.Generic;
using Coika.Core;
using Coika.Data;
using Coika.Fx;
using Coika.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks that the device tier reaches the scene objects (issue #39): the post-processing Volume and the particle
    /// count follow <see cref="QualityApplier"/>, and a missing tier means full quality.
    /// </summary>
    public class QualityApplierPlayModeTests : HarnessTestBase
    {
        private const int BURST_COUNT = 8;

        private readonly List<Object> _created = new();
        private ParticleSpawner _spawner;
        private Volume _volume;

        /// <summary>
        /// Builds a spawner and an enabled Volume.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();
            _created.Add(config);
            _spawner = TestParticleSpawner.Create(_created, config);

            var host = new GameObject("TestVolume");
            _created.Add(host);
            _volume = host.AddComponent<Volume>();
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
        /// On a Low device the Volume is off and a burst emits fewer particles than on a Normal one.
        /// </summary>
        [Test]
        public void Apply_LowTier_DisablesTheVolumeAndLowersTheParticleCount()
        {
            QualityApplier.Apply(TierFor(2, 2048), _spawner, _volume);
            _spawner.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, BURST_COUNT);

            Assert.IsFalse(_volume.enabled);
            Assert.Less(_spawner.CountMultiplier, 1f);
            Assert.AreEqual(Mathf.RoundToInt(BURST_COUNT * QualityTierService.LOW_PARTICLE_FACTOR), _spawner.LiveParticleCount);
        }

        /// <summary>
        /// On a Normal device the Volume stays on and a burst emits the full count.
        /// </summary>
        [Test]
        public void Apply_NormalTier_KeepsTheVolumeAndFullParticles()
        {
            QualityApplier.Apply(TierFor(8, 6144), _spawner, _volume);
            _spawner.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, BURST_COUNT);

            Assert.IsTrue(_volume.enabled);
            Assert.AreEqual(1f, _spawner.CountMultiplier);
            Assert.AreEqual(BURST_COUNT, _spawner.LiveParticleCount);
        }

        /// <summary>
        /// Without a tier (a scene without Boot) the scene runs at full quality, even after a Low tier was applied.
        /// </summary>
        [Test]
        public void Apply_NoTier_RestoresFullQuality()
        {
            QualityApplier.Apply(TierFor(2, 2048), _spawner, _volume);

            QualityApplier.Apply(null, _spawner, _volume);

            Assert.IsTrue(_volume.enabled);
            Assert.AreEqual(1f, _spawner.CountMultiplier);
        }

        /// <summary>
        /// A scene with no spawner and no Volume is skipped without an error.
        /// </summary>
        [Test]
        public void Apply_MissingObjects_DoesNothing()
        {
            Assert.DoesNotThrow(() => QualityApplier.Apply(TierFor(2, 2048), null, null));
        }

        /// <summary>
        /// Builds the real tier service for a fake device.
        /// </summary>
        /// <param name="processors">The fake processor count.</param>
        /// <param name="memoryMb">The fake memory in megabytes.</param>
        /// <returns>The tier for that device.</returns>
        private static IQualityTier TierFor(int processors, int memoryMb)
        {
            return new QualityTierService(new FakeSystemInfo(processors, memoryMb));
        }
    }
}
