namespace Coika.Core
{
    /// <summary>
    /// Backend of the Editor and of platforms without haptics: it does nothing.
    /// </summary>
    public sealed class NullHapticsBackend : IHapticsBackend
    {
        /// <summary>
        /// Ignores the request.
        /// </summary>
        /// <param name="kind">The effect, ignored.</param>
        public void Play(HapticKind kind)
        {
        }
    }
}
