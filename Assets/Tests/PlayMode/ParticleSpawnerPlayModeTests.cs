using System;
using System.Collections.Generic;
using Coika.Data;
using Coika.Fx;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the real <see cref="ParticleSpawner"/> of issue #32: what each effect emits (position, colour, size,
    /// lifetime), Reduce Shake, the quality multiplier, the hard caps and that bursting allocates nothing.
    /// </summary>
    public class ParticleSpawnerPlayModeTests : HarnessTestBase
    {
        private const int CAP = 40;
        private const int RING_CAP = 3;
        private const int BURSTS = 1000;
        private const float PIXELS_PER_UNIT = 16f;
        private const float TOLERANCE = 0.0001f;

        private readonly List<Object> _created = new();
        private FeedbackConfig _config;
        private ParticleSpawner _spawner;
        private ParticleSystem _particles;
        private ParticleSystem _rings;

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
            var systems = _spawner.GetComponentsInChildren<ParticleSystem>();
            _particles = systems[0];
            _rings = systems[1];
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
        /// A merge burst particle is where the burst was asked for (snapped to the pixel grid), in the asked colour,
        /// with the configured size and lifetime.
        /// </summary>
        [Test]
        public void Burst_OfMergeBurst_EmitsSnappedColouredParticlesWithConfiguredSizeAndLifetime()
        {
            _spawner.Burst(FxKind.MergeBurst, new Vector2(1.01f, 2.02f), Color.red, 3);

            var emitted = ReadParticles(_particles);
            Assert.AreEqual(3, emitted.Length);
            foreach (var particle in emitted)
            {
                Assert.AreEqual(1f, particle.position.x, TOLERANCE, "X snapped to 1/16.");
                Assert.AreEqual(2f, particle.position.y, TOLERANCE, "Y snapped to 1/16.");
                Assert.AreEqual((Color32)Color.red, particle.startColor);
                Assert.AreEqual(_config.MergeBurstSize / PIXELS_PER_UNIT, particle.startSize, TOLERANCE);
                Assert.AreEqual(_config.MergeBurstLifetime, particle.startLifetime, TOLERANCE);
            }
        }

        /// <summary>
        /// Dust particles are 3 px sprites that live as configured.
        /// </summary>
        [Test]
        public void Burst_OfLandingDust_EmitsThreePixelParticles()
        {
            _spawner.Burst(FxKind.LandingDust, Vector2.zero, Color.white, _config.LandDustCount);

            var emitted = ReadParticles(_particles);
            Assert.AreEqual(_config.LandDustCount, emitted.Length);
            Assert.AreEqual(3f / PIXELS_PER_UNIT, emitted[0].startSize, TOLERANCE);
            Assert.AreEqual(_config.LandDustLifetime, emitted[0].startLifetime, TOLERANCE);
        }

        /// <summary>
        /// Confetti is emitted as particles that live as long as the confetti lifetime.
        /// </summary>
        [Test]
        public void Burst_OfConfetti_EmitsParticlesWithTheConfettiLifetime()
        {
            _spawner.Burst(FxKind.Confetti, Vector2.zero, Color.cyan, _config.ConfettiCount);

            var emitted = ReadParticles(_particles);
            Assert.AreEqual(Mathf.Min(_config.ConfettiCount, CAP), emitted.Length);
            Assert.AreEqual(_config.ConfettiLifetime, emitted[0].startLifetime, TOLERANCE);
            Assert.AreEqual((Color32)Color.cyan, emitted[0].startColor);
        }

        /// <summary>
        /// Each ring effect emits one white ring with its own diameter and duration, and no particle, whatever the
        /// count asked.
        /// </summary>
        [TestCase(FxKind.FlashRing)]
        [TestCase(FxKind.SupernovaFlash)]
        [TestCase(FxKind.Shockwave)]
        public void Burst_OfARingEffect_EmitsOneWhiteRingWithItsOwnSize(FxKind kind)
        {
            _spawner.Burst(kind, new Vector2(0.5f, 0.5f), Color.red, 9);

            var emitted = ReadParticles(_rings);
            Assert.AreEqual(1, emitted.Length);
            Assert.AreEqual(0, _spawner.LiveParticleCount);
            Assert.AreEqual((Color32)Color.white, emitted[0].startColor, "Rings are always white.");
            Assert.AreEqual(RingDiameter(kind), emitted[0].startSize, TOLERANCE);
            Assert.AreEqual(RingDuration(kind), emitted[0].startLifetime, TOLERANCE);
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

            Assert.AreEqual(Mathf.RoundToInt(10 * _config.ReduceMotionCountFactor), _spawner.LiveParticleCount);
            Assert.AreEqual(1, _spawner.LiveRingCount);
        }

        /// <summary>
        /// A count multiplier of zero (lowest quality) emits no particles, a small one still emits one, and a
        /// negative one counts as zero.
        /// </summary>
        [Test]
        public void Burst_WithQualityMultiplier_ScalesTheCount()
        {
            _spawner.CountMultiplier = 0f;
            _spawner.Burst(FxKind.LandingDust, Vector2.zero, Color.white, 4);
            Assert.AreEqual(0, _spawner.LiveParticleCount);

            _spawner.CountMultiplier = -3f;
            Assert.AreEqual(0f, _spawner.CountMultiplier);

            _spawner.CountMultiplier = 0.1f;
            _spawner.Burst(FxKind.LandingDust, Vector2.zero, Color.white, 4);
            Assert.AreEqual(1, _spawner.LiveParticleCount);
        }

        /// <summary>
        /// Bursts past the caps leave exactly as many live particles and rings as the caps allow.
        /// </summary>
        [Test]
        public void Burst_PastTheCaps_FillsThemExactly()
        {
            for (var burst = 0; burst < 10; burst++)
            {
                _spawner.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, _config.MergeBurstMaxCount);
                _spawner.Burst(FxKind.FlashRing, Vector2.zero, Color.white, 1);

                Assert.LessOrEqual(_spawner.LiveParticleCount, CAP);
                Assert.LessOrEqual(_spawner.LiveRingCount, RING_CAP);
            }

            Assert.AreEqual(CAP, _spawner.LiveParticleCount, "The cap is reached, not just respected.");
            Assert.AreEqual(RING_CAP, _spawner.LiveRingCount);
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
        /// Bursting before <see cref="ParticleSpawner.Initialize"/> does nothing, and an unknown effect throws.
        /// </summary>
        [Test]
        public void Burst_BeforeInitializeOrWithUnknownKind_DoesNothingOrThrows()
        {
            var root = new GameObject("Uninitialized");
            _created.Add(root);
            var raw = root.AddComponent<ParticleSpawner>();

            Assert.DoesNotThrow(() => raw.Burst(FxKind.MergeBurst, Vector2.zero, Color.red, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _spawner.Burst((FxKind)999, Vector2.zero, Color.red, 5));
        }

        /// <summary>
        /// Initializing needs a config and both particle systems.
        /// </summary>
        [Test]
        public void Initialize_WithoutConfigOrSystems_Throws()
        {
            var root = new GameObject("Empty");
            _created.Add(root);
            var raw = root.AddComponent<ParticleSpawner>();

            Assert.Throws<ArgumentNullException>(() => raw.Initialize(null));
            Assert.Throws<InvalidOperationException>(() => raw.Initialize(_config));
        }

        /// <summary>
        /// A thousand bursts of every effect allocate nothing.
        /// </summary>
        [Test]
        public void Burst_OfEveryEffect_AllocatesNothing()
        {
            var kinds = (FxKind[])Enum.GetValues(typeof(FxKind));
            foreach (var kind in kinds)
            {
                _spawner.Burst(kind, Vector2.zero, Color.red, 8);
            }

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < BURSTS; i++)
                {
                    _spawner.Burst(kinds[i % kinds.Length], new Vector2(i * 0.01f, 1f), Color.cyan, 8);
                }
            });

            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT);
        }

        /// <summary>
        /// Reads the live particles of a system.
        /// </summary>
        /// <param name="system">The system.</param>
        /// <returns>A copy of its live particles.</returns>
        private static ParticleSystem.Particle[] ReadParticles(ParticleSystem system)
        {
            var particles = new ParticleSystem.Particle[system.particleCount];
            system.GetParticles(particles);
            return particles;
        }

        /// <summary>
        /// Gives the configured diameter of a ring effect.
        /// </summary>
        /// <param name="kind">A ring effect.</param>
        /// <returns>Its diameter in world units.</returns>
        private float RingDiameter(FxKind kind)
        {
            return kind switch
            {
                FxKind.FlashRing => _config.MergeRingDiameter,
                FxKind.SupernovaFlash => _config.SupernovaFlashDiameter,
                _ => _config.SupernovaRingDiameter
            };
        }

        /// <summary>
        /// Gives the configured duration of a ring effect.
        /// </summary>
        /// <param name="kind">A ring effect.</param>
        /// <returns>Its duration in seconds.</returns>
        private float RingDuration(FxKind kind)
        {
            return kind switch
            {
                FxKind.FlashRing => _config.MergeRingDuration,
                FxKind.SupernovaFlash => _config.SupernovaFlashDuration,
                _ => _config.SupernovaRingDuration
            };
        }
    }
}
