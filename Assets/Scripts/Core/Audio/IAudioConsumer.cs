namespace Coika.Core
{
    /// <summary>
    /// Implemented by a scene root that plays sounds. The Boot installer hands the audio service over right after the
    /// scene loads, next to <see cref="ISettingsConsumer"/>, so scenes never look it up and there is no static instance.
    /// </summary>
    public interface IAudioConsumer
    {
        /// <summary>
        /// Receives the shared audio service, before the consumer builds its objects.
        /// </summary>
        /// <param name="audio">The audio service owned by the Boot installer.</param>
        void UseAudio(IAudioService audio);
    }
}
