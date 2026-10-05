namespace Coika.Core.Animation
{
    /// <summary>
    /// The clock of one animation: whether it runs, how long it has run and how long it lasts. It is a struct with
    /// no references, so keeping several of them costs no allocation, and it never reads the real clock: the caller
    /// feeds it the time that passed.
    /// </summary>
    public struct TweenTimer
    {
        private float _elapsed;
        private float _duration;

        /// <summary>Whether the animation has started and has not reached its duration yet.</summary>
        public bool IsActive { get; private set; }

        /// <summary>How far the animation is, from 0 at the start to 1 at the end.</summary>
        public readonly float Progress => Tween.Progress(_elapsed, _duration);

        /// <summary>
        /// Starts the animation from its beginning. A duration of zero or less ends it on the first
        /// <see cref="Advance"/>.
        /// </summary>
        /// <param name="duration">Length of the animation in seconds.</param>
        public void Start(float duration)
        {
            _elapsed = 0f;
            _duration = duration;
            IsActive = true;
        }

        /// <summary>
        /// Stops the animation at once, as if it had never started.
        /// </summary>
        public void Stop()
        {
            _elapsed = 0f;
            IsActive = false;
        }

        /// <summary>
        /// Moves the animation forward and ends it when it reaches its duration. Does nothing when it is not active.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last call.</param>
        public void Advance(float deltaTime)
        {
            if (!IsActive)
            {
                return;
            }

            _elapsed += deltaTime;
            IsActive = _elapsed < _duration;
        }
    }
}
