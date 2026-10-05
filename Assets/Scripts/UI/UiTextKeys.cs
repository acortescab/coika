namespace Coika.UI
{
    /// <summary>
    /// Keys of the "UI" string table that code picks at runtime (S-94). The labels that never change are bound in
    /// the prefab by the Editor tool; these are the ones a reusable view shows with different texts. The English
    /// text of every key is in the Editor-only <c>UiStrings</c>.
    /// </summary>
    public static class UiTextKeys
    {
        /// <summary>Name of the string table.</summary>
        public const string TABLE_NAME = "UI";

        /// <summary>Title of the Restart confirmation.</summary>
        public const string CONFIRM_RESTART_TITLE = "confirm.restart.title";

        /// <summary>Message of the Restart confirmation.</summary>
        public const string CONFIRM_RESTART_MESSAGE = "confirm.restart.message";

        /// <summary>Title of the Menu confirmation.</summary>
        public const string CONFIRM_MENU_TITLE = "confirm.menu.title";

        /// <summary>Message of the Menu confirmation.</summary>
        public const string CONFIRM_MENU_MESSAGE = "confirm.menu.message";

        /// <summary>Title of the Reset progress confirmation.</summary>
        public const string CONFIRM_RESET_TITLE = "confirm.reset.title";

        /// <summary>Message of the Reset progress confirmation.</summary>
        public const string CONFIRM_RESET_MESSAGE = "confirm.reset.message";

        /// <summary>Text of a toggle that is on.</summary>
        public const string SETTINGS_ON = "settings.on";

        /// <summary>Text of a toggle that is off.</summary>
        public const string SETTINGS_OFF = "settings.off";
    }
}
