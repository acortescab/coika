using System;

namespace Coika.UI
{
    /// <summary>
    /// Shared timing of the UI animations: an injected clock, a base duration, a motion scale for reduced motion
    /// and the running state. Plain logic with no Unity types, so a fake clock tests the animations exactly. A
    /// subclass only adds its curve.
    /// </summary>
    public abstract class TimedAnimation
    {
        private readonly Func<double> _clock;
        private readonly float _baseDuration;

        private float _duration;
        private double _start;

        /// <summary>
        /// Creates an idle animation at normal motion.
        /// </summary>
        /// <param name="clock">Time source in seconds (unscaled in the game).</param>
        /// <param name="baseDuration">Seconds the animation takes at normal motion.</param>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
        protected TimedAnimation(Func<double> clock, float baseDuration)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _baseDuration = baseDuration;
            _duration = baseDuration;
        }

        /// <summary>True while the animation is still moving.</summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Scales the duration of the next runs, for reduced motion.
        /// </summary>
        /// <param name="scale">1 for normal motion; 0 makes the animation instant.</param>
        public void SetScale(float scale)
        {
            _duration = _baseDuration * scale;
        }

        /// <summary>Seconds a run takes now, after the motion scale. 0 or less means instant.</summary>
        protected float Duration => _duration;

        /// <summary>
        /// Starts a run now.
        /// </summary>
        protected void Begin()
        {
            _start = _clock();
            Running = true;
        }

        /// <summary>
        /// Stops the run at once.
        /// </summary>
        protected void End()
        {
            Running = false;
        }

        /// <summary>
        /// Gives how far the run is, from 0 at its start to 1 or more at its end.
        /// </summary>
        /// <returns>The progress of the run; only meaningful while <see cref="Running"/> and the duration is above 0.</returns>
        protected double Progress()
        {
            return (_clock() - _start) / _duration;
        }
    }
}
