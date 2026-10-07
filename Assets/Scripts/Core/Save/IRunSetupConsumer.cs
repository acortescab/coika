namespace Coika.Core
{
    /// <summary>
    /// Implemented by a scene root that needs the run setup. The Boot installer hands it over right after the scene
    /// loads, so the chosen mode crosses the scene load without statics or a lookup by type.
    /// </summary>
    public interface IRunSetupConsumer
    {
        /// <summary>
        /// Receives the run setup, before the consumer builds its objects.
        /// </summary>
        /// <param name="setup">The setup shared by the Menu and the Game scene.</param>
        void UseRunSetup(RunSetup setup);
    }
}
