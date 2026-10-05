namespace Coika.Fx
{
    /// <summary>
    /// Briefly slows time down. The seam between <see cref="ScreenFxDirector"/> and <see cref="TimeScaleOwner"/>,
    /// so tests can record the requests.
    /// </summary>
    public interface ISlowMo
    {
        /// <summary>
        /// Starts a slow-mo that lasts a number of real seconds. Ignored while the game is paused.
        /// </summary>
        /// <param name="scale">Time scale during the slow-mo.</param>
        /// <param name="duration">Real (unscaled) seconds it lasts.</param>
        void Begin(float scale, float duration);

        /// <summary>
        /// Ends the slow-mo at once and gives the normal speed back, unless the game is paused.
        /// </summary>
        void Cancel();
    }
}
