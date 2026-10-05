namespace Coika.Core
{
    /// <summary>
    /// The platform side of the haptics: the only code that talks to the device. It keeps the throttle and the
    /// setting logic of <see cref="Haptics"/> in plain C# that tests can run with a fake backend (S-20).
    /// </summary>
    public interface IHapticsBackend
    {
        /// <summary>
        /// Plays the effect on the device, without allocating. May throw when the platform call fails.
        /// </summary>
        /// <param name="kind">The effect to play.</param>
        void Play(HapticKind kind);
    }
}
