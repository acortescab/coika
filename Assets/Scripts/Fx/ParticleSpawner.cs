using System;
using Coika.Data;
using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// Draws every effect of <see cref="FxKind"/> with two shared particle systems, one for the pixel particles and one
    /// for the rings, both emitted with <see cref="ParticleSystem.Emit(ParticleSystem.EmitParams, int)"/>. Nothing is
    /// instantiated or destroyed during play, and the emit parameters are structs, so a burst allocates nothing. The
    /// systems have a hard cap (<see cref="FeedbackConfig.MaxLiveParticles"/>, <see cref="FeedbackConfig.MaxLiveRings"/>):
    /// when it is reached Unity removes the oldest particles first. Sizes are set in reference pixels (1/16 world
    /// unit), positions are snapped to the pixel grid and the particles are point-sampled, so the look matches the pixel art.
    /// </summary>
    public class ParticleSpawner : MonoBehaviour, IParticleSpawner
    {
        private const float PIXELS_PER_UNIT = 16f;

        [SerializeField]
        private ParticleSystem _particles;
        [SerializeField]
        private ParticleSystem _rings;

        private FeedbackConfig _config;
        private float _countMultiplier = 1f;
        private bool _reduceMotion;

        /// <summary>Number of pixel particles and rings alive right now.</summary>
        public int LiveCount => (_particles != null ? _particles.particleCount : 0) + (_rings != null ? _rings.particleCount : 0);

        /// <summary>Number of pixel particles alive right now.</summary>
        public int LiveParticleCount => _particles != null ? _particles.particleCount : 0;

        /// <summary>Number of rings alive right now.</summary>
        public int LiveRingCount => _rings != null ? _rings.particleCount : 0;

        /// <summary>
        /// Multiplier of every particle count, for a lower quality tier. 1 is full, 0 emits no particles (rings stay).
        /// </summary>
        public float CountMultiplier
        {
            get => _countMultiplier;
            set => _countMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Applies the Reduce Shake setting: it scales the particle counts by <see cref="FeedbackConfig.ReduceMotionCountFactor"/>.
        /// </summary>
        public bool ReduceMotion
        {
            get => _reduceMotion;
            set => _reduceMotion = value;
        }

        /// <summary>
        /// Applies the caps and warms the particle buffers up, so the first effect of a run does not allocate. Call it
        /// once in the load phase, before the first merge.
        /// </summary>
        /// <param name="config">Source of the caps and of the effect tuning.</param>
        /// <exception cref="ArgumentNullException">The config is null.</exception>
        /// <exception cref="InvalidOperationException">The two particle systems are not assigned.</exception>
        public void Initialize(FeedbackConfig config)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            if (_particles == null || _rings == null)
            {
                throw new InvalidOperationException("The ParticleSpawner needs a particle system for the particles and one for the rings.");
            }

            Prewarm(_particles, config.MaxLiveParticles);
            Prewarm(_rings, config.MaxLiveRings);
        }

        /// <summary>
        /// Removes every live particle, so a new run starts without leftovers.
        /// </summary>
        public void ResetAll()
        {
            if (_particles != null)
            {
                _particles.Clear();
            }

            if (_rings != null)
            {
                _rings.Clear();
            }
        }

        /// <summary>
        /// Emits one effect. Does nothing before <see cref="Initialize"/>.
        /// </summary>
        /// <param name="kind">The effect.</param>
        /// <param name="position">World position, snapped to the pixel grid.</param>
        /// <param name="color">Tint of the particles; rings and flashes are always white.</param>
        /// <param name="count">Requested particle count before scaling; ignored for single-ring effects.</param>
        /// <exception cref="ArgumentOutOfRangeException">The kind is not a known effect.</exception>
        public void Burst(FxKind kind, Vector2 position, Color color, int count)
        {
            if (_config == null)
            {
                return;
            }

            switch (kind)
            {
                case FxKind.MergeBurst:
                    EmitParticles(position, color, ScaleCount(count), _config.MergeBurstSize, _config.MergeBurstLifetime);
                    break;
                case FxKind.LandingDust:
                    EmitParticles(position, color, ScaleCount(count), _config.LandDustSize, _config.LandDustLifetime);
                    break;
                case FxKind.Confetti:
                    EmitParticles(position, color, ScaleCount(count), _config.MergeBurstSize, _config.ConfettiLifetime);
                    break;
                case FxKind.FlashRing:
                    EmitRing(position, _config.MergeRingDiameter, _config.MergeRingDuration);
                    break;
                case FxKind.SupernovaFlash:
                    EmitRing(position, _config.SupernovaFlashDiameter, _config.SupernovaFlashDuration);
                    break;
                case FxKind.Shockwave:
                    EmitRing(position, _config.SupernovaRingDiameter, _config.SupernovaRingDuration);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown effect.");
            }
        }

        /// <summary>
        /// Applies the quality multiplier and the Reduce Shake factor to a requested count.
        /// </summary>
        /// <param name="count">Requested count.</param>
        /// <returns>0 when the multiplier is 0 or no particle was requested, otherwise at least 1.</returns>
        private int ScaleCount(int count)
        {
            if (_countMultiplier <= 0f || count <= 0)
            {
                return 0;
            }

            var factor = _countMultiplier * (_reduceMotion ? _config.ReduceMotionCountFactor : 1f);
            return Mathf.Max(1, Mathf.RoundToInt(count * factor));
        }

        /// <summary>
        /// Emits pixel particles that start at the position and fly with the speed of the system.
        /// </summary>
        private void EmitParticles(Vector2 position, Color color, int count, float sizeInPixels, float lifetime)
        {
            if (count <= 0)
            {
                return;
            }

            var emit = new ParticleSystem.EmitParams
            {
                position = Snap(position),
                startColor = color,
                startSize = sizeInPixels / PIXELS_PER_UNIT,
                startLifetime = lifetime
            };
            _particles.Emit(emit, count);
        }

        /// <summary>
        /// Emits one white ring that grows and fades over its lifetime (the curves are on the prefab).
        /// </summary>
        private void EmitRing(Vector2 position, float diameter, float duration)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = Snap(position),
                startColor = Color.white,
                startSize = diameter,
                startLifetime = duration
            };
            _rings.Emit(emit, 1);
        }

        /// <summary>
        /// Rounds a position to the pixel grid.
        /// </summary>
        private static Vector3 Snap(Vector2 position)
        {
            return new Vector3(
                Mathf.Round(position.x * PIXELS_PER_UNIT) / PIXELS_PER_UNIT,
                Mathf.Round(position.y * PIXELS_PER_UNIT) / PIXELS_PER_UNIT,
                0f);
        }

        /// <summary>
        /// Sets the cap of a system and emits it full once, so its buffers are allocated before play.
        /// </summary>
        private static void Prewarm(ParticleSystem system, int cap)
        {
            var main = system.main;
            main.maxParticles = cap;
            system.Play();
            system.Emit(cap);
            system.Clear();
        }
    }
}
