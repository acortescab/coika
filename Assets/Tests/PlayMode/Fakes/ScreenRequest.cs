namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// One recorded request to the shake or the slow-mo.
    /// </summary>
    public readonly struct ScreenRequest
    {
        /// <summary>Creates the record.</summary>
        /// <param name="amplitude">Amplitude of a shake, or scale of a slow-mo.</param>
        /// <param name="duration">Seconds it lasts.</param>
        public ScreenRequest(float amplitude, float duration)
        {
            Amplitude = amplitude;
            Duration = duration;
        }

        /// <summary>Amplitude of a shake, or scale of a slow-mo.</summary>
        public float Amplitude { get; }

        /// <summary>Seconds it lasts.</summary>
        public float Duration { get; }
    }
}
