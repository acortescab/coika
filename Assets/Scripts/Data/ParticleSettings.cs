using System;
using UnityEngine;

namespace Coika.Data
{
    /// <summary>
    /// Tuning of the pooled particles and rings (GDD §9): caps, counts, sizes and lifetimes of the merge burst, the
    /// landing dust, the supernova and the confetti. A group of <see cref="FeedbackConfig"/>.
    /// </summary>
    [Serializable]
    public class ParticleSettings
    {
        [SerializeField, Min(1)]
        private int _maxLiveParticles = 160;
        [SerializeField, Min(1)]
        private int _maxLiveRings = 8;
        [SerializeField, Range(0f, 1f)]
        private float _reduceMotionCountFactor = 0.5f;
        [SerializeField, Min(1)]
        private int _mergeBurstMinCount = 6;
        [SerializeField, Min(1)]
        private int _mergeBurstMaxCount = 10;
        [SerializeField, Min(0.01f)]
        private float _mergeBurstSize = 2f;
        [SerializeField, Min(0.01f)]
        private float _mergeBurstLifetime = 0.4f;
        [SerializeField, Min(0.01f)]
        private float _mergeRingDiameter = 2f;
        [SerializeField, Min(0.01f)]
        private float _mergeRingDuration = 0.1f;
        [SerializeField, Min(1)]
        private int _landDustCount = 4;
        [SerializeField, Min(0.01f)]
        private float _landDustSize = 3f;
        [SerializeField, Min(0.01f)]
        private float _landDustLifetime = 0.3f;
        [SerializeField, Min(0.01f)]
        private float _supernovaFlashDiameter = 12f;
        [SerializeField, Min(0.01f)]
        private float _supernovaFlashDuration = 0.15f;
        [SerializeField, Min(0.01f)]
        private float _supernovaRingDiameter = 10f;
        [SerializeField, Min(0.01f)]
        private float _supernovaRingDuration = 0.5f;
        [SerializeField, Min(1)]
        private int _confettiCount = 30;
        [SerializeField, Min(0.01f)]
        private float _confettiLifetime = 1.2f;

        /// <summary>Hard cap of live pixel particles; when it is reached the oldest are recycled first.</summary>
        public int MaxLiveParticles => _maxLiveParticles;

        /// <summary>Hard cap of live flash and shockwave rings; when it is reached the oldest are recycled first.</summary>
        public int MaxLiveRings => _maxLiveRings;

        /// <summary>Multiplier of every particle count while Reduce Shake is on.</summary>
        public float ReduceMotionCountFactor => _reduceMotionCountFactor;

        /// <summary>Particles of the merge burst of the lowest tier.</summary>
        public int MergeBurstMinCount => _mergeBurstMinCount;

        /// <summary>Particles of the merge burst of the highest tier.</summary>
        public int MergeBurstMaxCount => _mergeBurstMaxCount;

        /// <summary>Size in reference pixels (1/16 world unit) of a merge burst particle.</summary>
        public float MergeBurstSize => _mergeBurstSize;

        /// <summary>Seconds a merge burst particle lives.</summary>
        public float MergeBurstLifetime => _mergeBurstLifetime;

        /// <summary>Largest diameter in world units of the white flash ring of a merge.</summary>
        public float MergeRingDiameter => _mergeRingDiameter;

        /// <summary>Seconds of the flash ring of a merge.</summary>
        public float MergeRingDuration => _mergeRingDuration;

        /// <summary>Particles of a landing dust puff.</summary>
        public int LandDustCount => _landDustCount;

        /// <summary>Size in reference pixels (1/16 world unit) of a dust particle.</summary>
        public float LandDustSize => _landDustSize;

        /// <summary>Seconds a dust particle lives.</summary>
        public float LandDustLifetime => _landDustLifetime;

        /// <summary>Diameter in world units of the white flash of a supernova.</summary>
        public float SupernovaFlashDiameter => _supernovaFlashDiameter;

        /// <summary>Seconds of the supernova flash.</summary>
        public float SupernovaFlashDuration => _supernovaFlashDuration;

        /// <summary>Largest diameter in world units of the supernova shockwave ring.</summary>
        public float SupernovaRingDiameter => _supernovaRingDiameter;

        /// <summary>Seconds of the supernova shockwave ring.</summary>
        public float SupernovaRingDuration => _supernovaRingDuration;

        /// <summary>Pixel particles of the new best confetti.</summary>
        public int ConfettiCount => _confettiCount;

        /// <summary>Seconds a confetti particle lives.</summary>
        public float ConfettiLifetime => _confettiLifetime;

        /// <summary>
        /// Computes the particle count of a merge burst, growing from the lowest to the highest tier.
        /// </summary>
        /// <param name="tier">Tier index of the piece the merge created.</param>
        /// <param name="tierCount">Number of tiers.</param>
        /// <returns>A count between <see cref="MergeBurstMinCount"/> and <see cref="MergeBurstMaxCount"/>.</returns>
        public int MergeBurstCount(int tier, int tierCount)
        {
            var t = tierCount > 1 ? Mathf.Clamp01(tier / (float)(tierCount - 1)) : 0f;
            return Mathf.RoundToInt(Mathf.Lerp(_mergeBurstMinCount, Mathf.Max(_mergeBurstMinCount, _mergeBurstMaxCount), t));
        }
    }
}
