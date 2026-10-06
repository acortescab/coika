using System;

namespace Coika.UI
{
    /// <summary>
    /// A short scale punch (1 up to a peak and back to 1) on an injected clock. It allocates nothing.
    /// </summary>
    public sealed class Punch : TimedAnimation
    {
        private readonly float _peak;

        /// <summary>
        /// Creates an idle punch.
        /// </summary>
        /// <param name="clock">Time source in seconds (unscaled in the game).</param>
        /// <param name="duration">Seconds the punch takes at normal motion.</param>
        /// <param name="peak">Largest scale, reached half way.</param>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
        public Punch(Func<double> clock, float duration, float peak)
            : base(clock, duration)
        {
            _peak = peak;
        }

        /// <summary>
        /// Starts a punch now, or does nothing when the duration is 0 or less.
        /// </summary>
        public void Start()
        {
            if (Duration > 0f)
            {
                Begin();
            }
        }

        /// <summary>
        /// Stops the punch at once.
        /// </summary>
        public void Cancel()
        {
            End();
        }

        /// <summary>
        /// Gives the scale for the clock's current time and ends the punch when it is over.
        /// </summary>
        /// <returns>1 when idle, otherwise a scale between 1 and the peak.</returns>
        public float Evaluate()
        {
            if (!Running)
            {
                return 1f;
            }

            var progress = Progress();
            if (progress >= 1d)
            {
                End();
                return 1f;
            }

            return 1f + ((_peak - 1f) * (float)Math.Sin(Math.PI * progress));
        }
    }
}
