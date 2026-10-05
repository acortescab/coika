namespace Coika.Fx
{
    /// <summary>
    /// Stands in for a shake or a flash that the scene does not have, so the director needs no null checks.
    /// </summary>
    public sealed class NullScreenEffects : IScreenShake, IScreenFlash
    {
        /// <summary>The shared instance; it holds no state.</summary>
        public static readonly NullScreenEffects Instance = new();

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="amplitude">Unused.</param>
        /// <param name="duration">Unused.</param>
        public void Shake(float amplitude, float duration)
        {
        }

        /// <summary>
        /// Does nothing; serves both the shake and the flash.
        /// </summary>
        public void Clear()
        {
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="duration">Unused.</param>
        public void Flash(float duration)
        {
        }    }
}
