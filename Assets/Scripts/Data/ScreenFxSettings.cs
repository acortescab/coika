using System;
using UnityEngine;

namespace Coika.Data
{
    /// <summary>
    /// Tuning of the screen effects (GDD §9): the heavy-merge threshold, the shake, the slow-mo and the screen
    /// flash. A group of <see cref="FeedbackConfig"/>.
    /// </summary>
    [Serializable]
    public class ScreenFxSettings
    {
        [SerializeField, Min(0)]
        private int _heavyMergeMinTier = 8;
        [SerializeField, Min(0f)]
        private float _shakeAmplitudePerTier = 0.05f;
        [SerializeField, Min(0.01f)]
        private float _shakeDuration = 0.2f;
        [SerializeField, Min(0f)]
        private float _supernovaShakeAmplitude = 0.25f;
        [SerializeField, Min(0.01f)]
        private float _supernovaShakeDuration = 0.3f;
        [SerializeField, Min(0f)]
        private float _shakeMaxAmplitude = 0.3f;
        [SerializeField, Range(0.05f, 1f)]
        private float _slowMoScale = 0.7f;
        [SerializeField, Min(0.01f)]
        private float _slowMoDuration = 0.1f;
        [SerializeField, Min(0.01f)]
        private float _screenFlashDuration = 0.15f;
        [SerializeField, Range(0f, 1f)]
        private float _screenFlashPeakAlpha = 0.9f;
        [SerializeField, Range(0.5f, 3f)]
        private float _maxFlashesPerSecond = 3f;

        /// <summary>Lowest tier index whose merge shakes the screen, slows time and plays the deep sound (GDD §9).</summary>
        public int HeavyMergeMinTier => _heavyMergeMinTier;

        /// <summary>Shake amplitude in world units added per tier above the one below <see cref="HeavyMergeMinTier"/>.</summary>
        public float ShakeAmplitudePerTier => _shakeAmplitudePerTier;

        /// <summary>Seconds a merge shake lasts.</summary>
        public float ShakeDuration => _shakeDuration;

        /// <summary>Fixed shake amplitude in world units of a supernova.</summary>
        public float SupernovaShakeAmplitude => _supernovaShakeAmplitude;

        /// <summary>Seconds the supernova shake lasts.</summary>
        public float SupernovaShakeDuration => _supernovaShakeDuration;

        /// <summary>Cap in world units of the combined offset of every active shake, per axis.</summary>
        public float ShakeMaxAmplitude => _shakeMaxAmplitude;

        /// <summary>Time scale during the slow-mo of a heavy merge.</summary>
        public float SlowMoScale => _slowMoScale;

        /// <summary>Real seconds of the slow-mo of a heavy merge.</summary>
        public float SlowMoDuration => _slowMoDuration;

        /// <summary>Seconds of the full-screen flash of a supernova.</summary>
        public float ScreenFlashDuration => _screenFlashDuration;

        /// <summary>Opacity of the full-screen flash at its peak.</summary>
        public float ScreenFlashPeakAlpha => _screenFlashPeakAlpha;

        /// <summary>Most flashes that may start in one second (GDD §16: no flashing above 3 Hz).</summary>
        public float MaxFlashesPerSecond => _maxFlashesPerSecond;
        /// <summary>
        /// Computes the shake amplitude of a merge: 0.05 x (tier - 7) with the defaults, 0 below the heavy tiers.
        /// </summary>
        /// <param name="tier">Tier index of the piece the merge created.</param>
        /// <returns>The amplitude in world units, 0 when the merge is not heavy.</returns>
        public float MergeShakeAmplitude(int tier)
        {
            return tier < _heavyMergeMinTier ? 0f : _shakeAmplitudePerTier * (tier - _heavyMergeMinTier + 1);
        }
    }
}
