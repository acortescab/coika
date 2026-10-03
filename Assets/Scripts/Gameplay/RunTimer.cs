using System;

namespace Coika.Gameplay
{
    /// <summary>
    /// Measures the play time of a run for the Game Over screen. It reads an injected clock, so it is tested
    /// without Unity and follows the clock the game gives it (scaled time, so a pause does not count).
    /// </summary>
    public sealed class RunTimer
    {
        private readonly Func<double> _clock;

        private double _startTime;
        private double _stopTime;

        /// <summary>
        /// Creates a stopped timer at 0 seconds.
        /// </summary>
        /// <param name="clock">Gives the current time in seconds; <c>() =&gt; Time.timeAsDouble</c> in the game.</param>
        /// <exception cref="ArgumentNullException">The clock is null.</exception>
        public RunTimer(Func<double> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>Whether the timer is counting.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Seconds since <see cref="Start"/>, frozen at <see cref="Stop"/>. It is 0 before the first start.
        /// </summary>
        public float ElapsedSeconds
        {
            get
            {
                var end = IsRunning ? _clock() : _stopTime;
                return (float)Math.Max(0d, end - _startTime);
            }
        }

        /// <summary>
        /// Starts counting from 0. Calling it again restarts the count.
        /// </summary>
        public void Start()
        {
            _startTime = _clock();
            _stopTime = _startTime;
            IsRunning = true;
        }

        /// <summary>
        /// Freezes the elapsed time. Safe to call when the timer is not running.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            _stopTime = _clock();
            IsRunning = false;
        }
    }
}
