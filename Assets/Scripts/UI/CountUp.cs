using System;

namespace Coika.UI
{
    /// <summary>
    /// Rolls an integer to a new value over a fixed time, on an injected clock. It always ends on the exact target
    /// and allocates nothing.
    /// </summary>
    public sealed class CountUp : TimedAnimation
    {
        private int _from;
        private int _target;

        /// <summary>
        /// Creates a counter showing 0.
        /// </summary>
        /// <param name="clock">Time source in seconds (unscaled in the game).</param>
        /// <param name="duration">Seconds a roll takes at normal motion.</param>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
        public CountUp(Func<double> clock, float duration)
            : base(clock, duration)
        {
        }

        /// <summary>The value to show now.</summary>
        public int Value { get; private set; }

        /// <summary>
        /// Starts rolling from the shown value to a new target. An instant duration or an unchanged target jumps.
        /// </summary>
        /// <param name="target">Value to end on.</param>
        public void SetTarget(int target)
        {
            if (Duration <= 0f || target == Value)
            {
                Snap(target);
                return;
            }

            _from = Value;
            _target = target;
            Begin();
        }

        /// <summary>
        /// Jumps to a value at once and stops any roll.
        /// </summary>
        /// <param name="value">Value to show.</param>
        public void Snap(int value)
        {
            End();
            _target = value;
            Value = value;
        }

        /// <summary>
        /// Advances the roll to the clock's current time.
        /// </summary>
        /// <returns>True when <see cref="Value"/> changed.</returns>
        public bool Tick()
        {
            if (!Running)
            {
                return false;
            }

            var progress = Progress();
            var previous = Value;
            if (progress >= 1d)
            {
                Snap(_target);
            }
            else
            {
                Value = _from + (int)((_target - _from) * progress);
            }

            return Value != previous;
        }
    }
}
