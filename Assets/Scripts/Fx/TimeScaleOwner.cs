using System;

namespace Coika.Fx
{
    /// <summary>
    /// The one writer of the time scale: it is 0 while paused, the slow-mo scale during a slow-mo, and 1 otherwise.
    /// A slow-mo never overrides the pause: it is refused while paused, and pausing cancels it, so after the resume
    /// the scale is 1. The slow-mo ends on the injected unscaled clock, so it lasts its real seconds whatever the
    /// scale. It writes the scale only when the wanted value changes, so it allocates nothing.
    /// </summary>
    public sealed class TimeScaleOwner : ISlowMo
    {
        private const float NORMAL_SCALE = 1f;

        private readonly ITimeScale _timeScale;
        private readonly Func<double> _unscaledClock;
        private bool _paused;
        private bool _slowMo;
        private float _slowScale;
        private double _slowEnd;

        /// <summary>
        /// Creates the owner and puts the time scale to its normal value.
        /// </summary>
        /// <param name="timeScale">The time scale to drive.</param>
        /// <param name="unscaledClock">Seconds that do not depend on the time scale.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public TimeScaleOwner(ITimeScale timeScale, Func<double> unscaledClock)
        {
            _timeScale = timeScale ?? throw new ArgumentNullException(nameof(timeScale));
            _unscaledClock = unscaledClock ?? throw new ArgumentNullException(nameof(unscaledClock));
            Apply();
        }

        /// <summary>Whether a slow-mo is running.</summary>
        public bool IsSlowMo => _slowMo;

        /// <summary>
        /// Gets or sets whether the game is paused. Pausing cancels a running slow-mo and sets the scale to 0;
        /// resuming sets it to 1.
        /// </summary>
        public bool Paused
        {
            get => _paused;
            set
            {
                _paused = value;
                if (value)
                {
                    _slowMo = false;
                }

                Apply();
            }
        }

        /// <summary>
        /// Starts a slow-mo. Ignored while paused or when the scale or the duration is not usable.
        /// </summary>
        /// <param name="scale">Time scale during the slow-mo, above 0.</param>
        /// <param name="duration">Real seconds it lasts.</param>
        public void Begin(float scale, float duration)
        {
            if (_paused || scale <= 0f || duration <= 0f)
            {
                return;
            }

            _slowMo = true;
            _slowScale = scale;
            _slowEnd = _unscaledClock() + duration;
            Apply();
        }

        /// <summary>
        /// Ends the slow-mo. The scale goes back to 1, or stays 0 while paused.
        /// </summary>
        public void Cancel()
        {
            _slowMo = false;
            Apply();
        }

        /// <summary>
        /// Ends the slow-mo when its real time is up. Call it every frame.
        /// </summary>
        public void Tick()
        {
            if (_slowMo && _unscaledClock() >= _slowEnd)
            {
                Cancel();
            }
        }

        /// <summary>
        /// Writes the wanted scale when it differs from the current one.
        /// </summary>
        private void Apply()
        {
            var wanted = _paused ? 0f : _slowMo ? _slowScale : NORMAL_SCALE;
            if (_timeScale.Value != wanted)
            {
                _timeScale.Value = wanted;
            }
        }
    }
}
