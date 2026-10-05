using System;

namespace Coika.Core
{
    /// <summary>
    /// Smooth attenuation of the music in dB: moves toward a target at a constant speed, so a duck of −6 dB over half
    /// a second and the return to 0 dB both ramp instead of jumping.
    /// </summary>
    public sealed class DuckEnvelope
    {
        private float _target;
        private float _speed; // dB per second; infinite for an immediate change

        /// <summary>Current attenuation in dB, 0 or negative.</summary>
        public float Current { get; private set; }

        /// <summary>
        /// Sets a new target.
        /// </summary>
        /// <param name="db">Target attenuation in dB. Positive values count as 0.</param>
        /// <param name="seconds">Time to get there. Zero or less applies it at once.</param>
        public void SetTarget(float db, float seconds)
        {
            _target = Math.Min(db, 0f);
            if (seconds <= 0f)
            {
                Current = _target;
                _speed = 0f;
                return;
            }

            _speed = Math.Abs(_target - Current) / seconds;
        }

        /// <summary>
        /// Moves the current value toward the target.
        /// </summary>
        /// <param name="deltaSeconds">Time since the last call, in seconds.</param>
        /// <returns>True when the value changed.</returns>
        public bool Advance(float deltaSeconds)
        {
            if (Current == _target)
            {
                return false;
            }

            var step = _speed * deltaSeconds;
            Current = Current < _target ? Math.Min(Current + step, _target) : Math.Max(Current - step, _target);
            return true;
        }
    }
}
