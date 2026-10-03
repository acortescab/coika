namespace Coika.Core
{
    /// <summary>
    /// Implemented by a scene root that needs the save system. The Boot installer hands it over right after the
    /// scene loads, so scenes never look it up (no lookup by type, no singleton).
    /// </summary>
    public interface ISaveConsumer
    {
        /// <summary>
        /// Receives the save system, before the consumer builds its objects.
        /// </summary>
        /// <param name="save">The loaded save system.</param>
        void UseSave(SaveSystem save);
    }
}
