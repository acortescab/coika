namespace Coika.Fx
{
    /// <summary>
    /// Shakes the camera rig. The seam between the code that decides when a shake happens
    /// (<see cref="ScreenFxDirector"/>) and the one that moves the camera, so tests can record the requests.
    /// </summary>
    public interface IScreenShake
    {
        /// <summary>
        /// Starts a shake that fades out; it adds to the shakes already running, up to a cap.
        /// </summary>
        /// <param name="amplitude">Largest offset in world units at the start.</param>
        /// <param name="duration">Seconds until the shake is over.</param>
        void Shake(float amplitude, float duration);

        /// <summary>
        /// Stops every shake and puts the camera rig back at rest.
        /// </summary>
        void Clear();
    }
}
