namespace Coika.Fx
{
    /// <summary>
    /// Flashes the whole screen. The seam between <see cref="FeedbackDirector"/> and the overlay, so tests can
    /// record the requests.
    /// </summary>
    public interface IScreenFlash
    {
        /// <summary>
        /// Asks for a white flash. The request is ignored when it would make flashes faster than the allowed rate.
        /// </summary>
        /// <param name="duration">Seconds the flash takes to fade out.</param>
        void Flash(float duration);

        /// <summary>
        /// Stops the flash that is running and hides the overlay.
        /// </summary>
        void Clear();
    }
}
