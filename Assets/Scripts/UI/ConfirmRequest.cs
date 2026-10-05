namespace Coika.UI
{
    /// <summary>
    /// What a confirmation dialog asks: the keys of its title and its message in the "UI" string table. A new
    /// confirmation (Reset progress, in M3) is a new value of this type, not a new view or a new code path.
    /// </summary>
    public readonly struct ConfirmRequest
    {
        /// <summary>
        /// Creates a request.
        /// </summary>
        /// <param name="titleKey">Key of the title.</param>
        /// <param name="messageKey">Key of the message.</param>
        public ConfirmRequest(string titleKey, string messageKey)
        {
            TitleKey = titleKey;
            MessageKey = messageKey;
        }

        /// <summary>Asks to restart the run.</summary>
        public static ConfirmRequest Restart => new ConfirmRequest(UiTextKeys.CONFIRM_RESTART_TITLE, UiTextKeys.CONFIRM_RESTART_MESSAGE);

        /// <summary>Asks to leave for the menu.</summary>
        public static ConfirmRequest Menu => new ConfirmRequest(UiTextKeys.CONFIRM_MENU_TITLE, UiTextKeys.CONFIRM_MENU_MESSAGE);

        /// <summary>Key of the title.</summary>
        public string TitleKey { get; }

        /// <summary>Key of the message.</summary>
        public string MessageKey { get; }
    }
}
