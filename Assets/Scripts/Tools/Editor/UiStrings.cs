using Coika.UI;

namespace Coika.Tools
{
    /// <summary>
    /// Keys and English texts of the HUD and Game Over strings (S-94). They live in the "UI" string table that
    /// <see cref="LocalizationSetup"/> creates and that <see cref="GameCanvasPrefabTool"/> binds the labels to.
    /// Spanish is added in M3 as another locale of the same table.
    /// </summary>
    public static class UiStrings
    {
        /// <summary>Name of the string table.</summary>
        public const string TableName = "UI";

        public const string HudBest = "hud.best";
        public const string GameOverTitle = "gameover.title";
        public const string GameOverScore = "gameover.score";
        public const string GameOverBest = "gameover.best";
        public const string GameOverNewBest = "gameover.new_best";
        public const string GameOverHighestTier = "gameover.highest_tier";
        public const string GameOverPieces = "gameover.pieces";
        public const string GameOverTime = "gameover.time";
        public const string GameOverRetry = "gameover.retry";
        public const string GameOverMenu = "gameover.menu";
        public const string LoadingText = "loading.text";
        public const string LoadingFailed = "loading.failed";
        public const string LoadingRetry = "loading.retry";
        public const string PauseTitle = "pause.title";
        public const string PauseResume = "pause.resume";
        public const string PauseRestart = "pause.restart";
        public const string PauseSettings = "pause.settings";
        public const string PauseMenu = "pause.menu";
        public const string ConfirmYes = "confirm.yes";
        public const string ConfirmCancel = "confirm.cancel";
        public const string ConfirmRestartTitle = UiTextKeys.CONFIRM_RESTART_TITLE;
        public const string ConfirmRestartMessage = UiTextKeys.CONFIRM_RESTART_MESSAGE;
        public const string ConfirmMenuTitle = UiTextKeys.CONFIRM_MENU_TITLE;
        public const string ConfirmMenuMessage = UiTextKeys.CONFIRM_MENU_MESSAGE;

        /// <summary>Every key with its English text.</summary>
        public static readonly (string Key, string English)[] All =
        {
            (HudBest, "BEST"),
            (GameOverTitle, "GAME OVER"),
            (GameOverScore, "SCORE"),
            (GameOverBest, "BEST"),
            (GameOverNewBest, "NEW BEST!"),
            (GameOverHighestTier, "HIGHEST TIER"),
            (GameOverPieces, "PIECES"),
            (GameOverTime, "TIME"),
            (GameOverRetry, "RETRY"),
            (GameOverMenu, "MENU"),
            (LoadingText, "LOADING..."),
            (LoadingFailed, "COULD NOT LOAD THE GAME"),
            (LoadingRetry, "RETRY"),
            (PauseTitle, "PAUSED"),
            (PauseResume, "RESUME"),
            (PauseRestart, "RESTART"),
            (PauseSettings, "SETTINGS"),
            (PauseMenu, "MENU"),
            (ConfirmYes, "YES"),
            (ConfirmCancel, "CANCEL"),
            (ConfirmRestartTitle, "RESTART?"),
            (ConfirmRestartMessage, "The current run will be lost."),
            (ConfirmMenuTitle, "LEAVE THE GAME?"),
            (ConfirmMenuMessage, "The current run will be lost."),
        };
    }
}
