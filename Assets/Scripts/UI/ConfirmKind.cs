namespace Coika.UI
{
    /// <summary>
    /// What a confirmation dialog was asked for. The owner of the <see cref="PausePresenter"/> switches on it once
    /// when the player confirms.
    /// </summary>
    public enum ConfirmKind
    {
        /// <summary>Start a fresh run.</summary>
        Restart,

        /// <summary>Leave for the menu.</summary>
        Menu,

        /// <summary>Erase bests, totals and discovered tiers.</summary>
        ResetProgress
    }
}
