using System;
using System.Collections.Generic;

namespace Coika.Core
{
    /// <summary>
    /// Rate limit of the landing sounds (GDD §11): at most one every 40 ms and at most 3 sounding at once. The time
    /// comes from an injected clock, never from the real one (S-63).
    /// </summary>
    public sealed class LandLimiter
    {
        /// <summary>Shortest time between two landing sounds, in seconds.</summary>
        public const double MIN_INTERVAL = 0.04;

        /// <summary>Most landing sounds that may play at the same time.</summary>
        public const int MAX_SIMULTANEOUS = 3;

        private readonly Func<double> _clock;
        private readonly List<double> _endTimes = new(MAX_SIMULTANEOUS);
        private double _lastAccepted = double.NegativeInfinity;

        /// <summary>
        /// Creates the limiter.
        /// </summary>
        /// <param name="clock">Returns the current time in seconds.</param>
        /// <exception cref="ArgumentNullException">The clock is null.</exception>
        public LandLimiter(Func<double> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// Decides whether a landing sound may play now and, if so, records it. A refused request is dropped
        /// silently by the caller.
        /// </summary>
        /// <param name="duration">Length of the sound in seconds, used to know when it stops sounding.</param>
        /// <returns>True when the sound may play.</returns>
        public bool TryAcquire(double duration)
        {
            var now = _clock();
            if (now - _lastAccepted < MIN_INTERVAL)
            {
                return false;
            }

            _endTimes.RemoveAll(end => end <= now);
            if (_endTimes.Count >= MAX_SIMULTANEOUS)
            {
                return false;
            }

            _lastAccepted = now;
            _endTimes.Add(now + Math.Max(duration, 0.0));
            return true;
        }
    }
}
