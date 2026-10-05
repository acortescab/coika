namespace Coika.Core
{
    /// <summary>
    /// Plays haptic feedback. Safe to call anywhere: it does nothing when the setting is off, in the Editor or on a
    /// device without a vibrator.
    /// </summary>
    public interface IHaptics
    {
        /// <summary>
        /// Plays one haptic effect, unless the setting is off, the call is throttled or the wrapper has disabled itself.
        /// </summary>
        /// <param name="kind">The effect to play.</param>
        void Play(HapticKind kind);
    }
}
