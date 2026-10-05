using System;
using UnityEngine;

namespace Coika.Data
{
    /// <summary>
    /// Tuning of the purely visual piece animations (GDD §9): the spawn pop, the drop stretch, the landing squash, the
    /// merge pop and shrink, and the game-over flash. A group of <see cref="FeedbackConfig"/>.
    /// </summary>
    [Serializable]
    public class PieceAnimationSettings
    {
        [SerializeField, Min(0f)]
        private float _spawnDuration = 0.15f;
        [SerializeField, Min(0f)]
        private float _spawnOvershoot = 1.70158f;
        [SerializeField, Range(0f, 0.5f)]
        private float _dropStretch = 0.12f;
        [SerializeField, Min(0.01f)]
        private float _dropStretchFullSpeed = 12f;
        [SerializeField, Min(0f)]
        private float _landImpulseThreshold = 2f;
        [SerializeField, Min(0f)]
        private float _landDuration = 0.1f;
        [SerializeField, Min(0f)]
        private float _landAmplitudePerImpulse = 0.02f;
        [SerializeField, Range(0f, 0.5f)]
        private float _landAmplitudeMax = 0.25f;
        [SerializeField, Range(0f, 1f)]
        private float _landReboundRatio = 0.5f;
        [SerializeField, Min(0f)]
        private float _mergePopDuration = 0.2f;
        [SerializeField, Min(1f)]
        private float _mergePopPeak = 1.2f;
        [SerializeField, Range(0.05f, 0.95f)]
        private float _mergePopPeakAt = 0.5f;
        [SerializeField, Min(0f)]
        private float _mergeShrinkDuration = 0.2f;
        [SerializeField, Min(1)]
        private int _mergeGhostPoolSize = 16;
        [SerializeField, Min(0.01f)]
        private float _gameOverFlashDuration = 0.2f;
        [SerializeField]
        private Color _gameOverFlashColor = new(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField, Min(0f)]
        private float _gameOverSweepDuration = 0.6f;

        /// <summary>Seconds a new piece takes to grow from nothing to full size.</summary>
        public float SpawnDuration => _spawnDuration;

        /// <summary>How far past full size the spawn pop goes (back-ease constant; 1.70158 is about 10 percent).</summary>
        public float SpawnOvershoot => _spawnOvershoot;

        /// <summary>Vertical stretch of a falling piece at full speed, as a fraction of its size.</summary>
        public float DropStretch => _dropStretch;

        /// <summary>Fall speed in units per second at which the drop stretch reaches its full amount.</summary>
        public float DropStretchFullSpeed => _dropStretchFullSpeed;

        /// <summary>Landing impulse below which a contact does not squash the piece, nor make dust or a sound.</summary>
        public float LandImpulseThreshold => _landImpulseThreshold;

        /// <summary>Seconds of the landing squash and stretch.</summary>
        public float LandDuration => _landDuration;

        /// <summary>Squash amplitude added per unit of landing impulse, before the clamp.</summary>
        public float LandAmplitudePerImpulse => _landAmplitudePerImpulse;

        /// <summary>Largest landing squash amplitude, as a fraction of the piece size.</summary>
        public float LandAmplitudeMax => _landAmplitudeMax;

        /// <summary>Size of the stretch after a landing squash, as a fraction of the squash amplitude.</summary>
        public float LandReboundRatio => _landReboundRatio;

        /// <summary>Seconds of the pop of a piece created by a merge.</summary>
        public float MergePopDuration => _mergePopDuration;

        /// <summary>Scale the merge pop reaches before settling at 1.</summary>
        public float MergePopPeak => _mergePopPeak;

        /// <summary>Fraction of the merge pop at which the peak happens.</summary>
        public float MergePopPeakAt => _mergePopPeakAt;

        /// <summary>Seconds the source pieces of a merge take to shrink to nothing.</summary>
        public float MergeShrinkDuration => _mergeShrinkDuration;

        /// <summary>Number of visual-only ghosts prewarmed for the shrinking source pieces of merges.</summary>
        public int MergeGhostPoolSize => _mergeGhostPoolSize;

        /// <summary>Seconds one piece stays tinted in the game-over flash.</summary>
        public float GameOverFlashDuration => _gameOverFlashDuration;

        /// <summary>Tint a piece reaches at the peak of its game-over flash.</summary>
        public Color GameOverFlashColor => _gameOverFlashColor;

        /// <summary>Seconds the game-over flash takes to sweep from the top piece to the bottom one.</summary>
        public float GameOverSweepDuration => _gameOverSweepDuration;

        /// <summary>
        /// Seconds after game over at which a piece flashes: 0 for a piece at the top of the jar, the sweep duration
        /// for one at the floor, linear in between and clamped outside the jar.
        /// </summary>
        /// <param name="y">World height of the piece.</param>
        /// <param name="top">World height of the top of the jar.</param>
        /// <param name="floor">World height of the floor.</param>
        /// <returns>The delay in seconds.</returns>
        public float GameOverDelay(float y, float top, float floor)
        {
            var fromTop = Mathf.Clamp01((top - y) / Mathf.Max(top - floor, 0.0001f));
            return fromTop * _gameOverSweepDuration;
        }

        /// <summary>
        /// Computes the squash amplitude of a landing, proportional to the impulse and clamped.
        /// </summary>
        /// <param name="impulse">Total normal impulse of the landing contact.</param>
        /// <returns>0 below the threshold, otherwise the amplitude up to <see cref="LandAmplitudeMax"/>.</returns>
        public float LandAmplitude(float impulse)
        {
            if (impulse < _landImpulseThreshold)
            {
                return 0f;
            }

            return Mathf.Min(impulse * _landAmplitudePerImpulse, _landAmplitudeMax);
        }
    }
}
