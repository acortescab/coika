using Coika.Fx;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// The slow-mo seam of the simulated director: it records every request in the fake screen effects, as the plain
    /// feedback does, and also hands it to the real <see cref="TimeScaleOwner"/>, so a test sees the time scale the game
    /// would have used.
    /// </summary>
    public sealed class SlowMoRelay : ISlowMo
    {
        private readonly ISlowMo _recorder;
        private readonly ISlowMo _owner;

        /// <summary>
        /// Creates the relay.
        /// </summary>
        /// <param name="recorder">Records the requests.</param>
        /// <param name="owner">Applies the requests to a time scale.</param>
        public SlowMoRelay(ISlowMo recorder, ISlowMo owner)
        {
            _recorder = recorder;
            _owner = owner;
        }

        /// <inheritdoc />
        public void Begin(float scale, float duration)
        {
            _recorder.Begin(scale, duration);
            _owner.Begin(scale, duration);
        }

        /// <inheritdoc />
        public void Cancel()
        {
            _recorder.Cancel();
            _owner.Cancel();
        }
    }
}
