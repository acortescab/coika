namespace Coika.Core
{
    /// <summary>
    /// Implemented by a scene root that needs the user settings. The Boot installer hands them over right after the
    /// scene loads, next to <see cref="ISaveConsumer"/>, so scenes never look them up (no lookup by type, no singleton).
    /// </summary>
    public interface ISettingsConsumer
    {
        /// <summary>
        /// Receives the shared settings, before the consumer builds its objects.
        /// </summary>
        /// <param name="settings">The settings service owned by the Boot installer.</param>
        void UseSettings(SettingsService settings);
    }
}
