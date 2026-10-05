namespace Coika.UI
{
    /// <summary>
    /// What a confirmation dialog asks: its <see cref="ConfirmKind"/> and the keys of its title and its message in
    /// the "UI" string table. A new confirmation is a new kind and a new value of this type,
    /// not a new view or a new handler.
    /// </summary>
    public readonly struct ConfirmRequest
    {
        /// <summary>
        /// Creates a request.
        /// </summary>
        /// <param name="kind">What the player is asked to confirm.</param>
        /// <param name="titleKey">Key of the title.</param>
        /// <param name="messageKey">Key of the message.</param>
        public ConfirmRequest(ConfirmKind kind, string titleKey, string messageKey)
        {
            Kind = kind;
            TitleKey = titleKey;
            MessageKey = messageKey;
        }

        /// <summary>Asks to restart the run.</summary>
        public static ConfirmRequest Restart => new ConfirmRequest(ConfirmKind.Restart, UiTextKeys.CONFIRM_RESTART_TITLE, UiTextKeys.CONFIRM_RESTART_MESSAGE);

        /// <summary>Asks to leave for the menu.</summary>
        public static ConfirmRequest Menu => new ConfirmRequest(ConfirmKind.Menu, UiTextKeys.CONFIRM_MENU_TITLE, UiTextKeys.CONFIRM_MENU_MESSAGE);

        /// <summary>Asks to erase the progress.</summary>
        public static ConfirmRequest ResetProgress => new ConfirmRequest(ConfirmKind.ResetProgress, UiTextKeys.CONFIRM_RESET_TITLE, UiTextKeys.CONFIRM_RESET_MESSAGE);

        /// <summary>What the player is asked to confirm.</summary>
        public ConfirmKind Kind { get; }

        /// <summary>Key of the title.</summary>
        public string TitleKey { get; }

        /// <summary>Key of the message.</summary>
        public string MessageKey { get; }
    }
}
