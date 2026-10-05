namespace Coika.Core
{
    /// <summary>
    /// Implemented by a scene root that plays haptics. The Boot installer hands the service over right after the scene
    /// loads, next to <see cref="IAudioConsumer"/>, so scenes never look it up and there is no static instance.
    /// </summary>
    public interface IHapticsConsumer
    {
        /// <summary>
        /// Receives the shared haptics service, before the consumer builds its objects.
        /// </summary>
        /// <param name="haptics">The haptics service owned by the Boot installer.</param>
        void UseHaptics(IHaptics haptics);
    }
}
