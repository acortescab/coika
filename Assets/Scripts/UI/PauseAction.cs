namespace Coika.UI
{
    /// <summary>
    /// A button of the pause menu. The <see cref="PauseView"/> raises one event with the action, so a new button is
    /// a new value here and a new serialized button, not a new event.
    /// </summary>
    public enum PauseAction
    {
        /// <summary>Go back to the run.</summary>
        Resume,

        /// <summary>Start a fresh run.</summary>
        Restart,

        /// <summary>Open the Settings screen.</summary>
        Settings,

        /// <summary>Leave for the menu.</summary>
        Menu
    }
}
