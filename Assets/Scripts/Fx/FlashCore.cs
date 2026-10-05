using System;
using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// The maths of the full-screen flash, with no Unity objects. It accepts a flash only when enough time has
    /// passed since the last accepted one, so flashes never exceed the allowed rate (GDD §16: 3 Hz); requests that
    /// come sooner are ignored. The flash fades out linearly from its peak.
    /// </summary>
    public sealed class FlashCore
    {
        private readonly double _minInterval;
        private readonly float _peakAlpha;
        private double _start = double.NegativeInfinity;
        private double _duration;

        /// <summary>
        /// Creates a core with no flash running.
        /// </summary>
        /// <param name="maxFlashesPerSecond">Most flashes that may start in one second, above 0.</param>
        /// <param name="peakAlpha">Opacity at the start of a flash, from 0 to 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">The rate is not above 0.</exception>
        public FlashCore(float maxFlashesPerSecond, float peakAlpha)
        {
            if (maxFlashesPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFlashesPerSecond), "The rate must be above 0.");
            }

            _minInterval = 1.0 / maxFlashesPerSecond;
            _peakAlpha = Mathf.Clamp01(peakAlpha);
        }

        /// <summary>
        /// Starts a flash unless the previous one began too recently.
        /// </summary>
        /// <param name="duration">Seconds the flash takes to fade out.</param>
        /// <param name="now">Current time in seconds.</param>
        /// <returns>True when the flash started, false when it was ignored.</returns>
        public bool TryFlash(float duration, double now)
        {
            if (duration <= 0f || now - _start < _minInterval)
            {
                return false;
            }

            _start = now;
            _duration = duration;
            return true;
        }

        /// <summary>
        /// Computes the opacity of the overlay at a time.
        /// </summary>
        /// <param name="now">Current time in seconds.</param>
        /// <returns>0 when no flash is running, otherwise a value fading from the peak to 0.</returns>
        public float Alpha(double now)
        {
            var elapsed = now - _start;
            if (elapsed < 0.0 || elapsed >= _duration)
            {
                return 0f;
            }

            return _peakAlpha * (1f - (float)(elapsed / _duration));
        }

        /// <summary>
        /// Stops the flash that is running and forgets the last one, so the next request is accepted.
        /// </summary>
        public void Clear()
        {
            _start = double.NegativeInfinity;
            _duration = 0.0;
        }
    }
}
