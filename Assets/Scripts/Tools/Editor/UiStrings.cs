using System.Collections.Generic;
using Coika.Core;
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
        public const string TableName = UiTextKeys.TABLE_NAME;

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
        public const string SettingsTitle = "settings.title";
        public const string SettingsMusic = "settings.music";
        public const string SettingsSfx = "settings.sfx";
        public const string SettingsHaptics = "settings.haptics";
        public const string SettingsGuideLine = "settings.guide_line";
        public const string SettingsReduceShake = "settings.reduce_shake";
        public const string SettingsFingerOffset = "settings.finger_offset";
        public const string SettingsLeftHanded = "settings.left_handed";
        public const string SettingsLanguage = "settings.language";
        public const string SettingsLanguageValue = "settings.language_value";
        public const string SettingsReset = "settings.reset";
        public const string SettingsBack = "settings.back";

        /// <summary>The string key of the name of each setting that has a row on the Settings screen.</summary>
        public static readonly IReadOnlyDictionary<SettingKey, string> SettingLabels = new Dictionary<SettingKey, string>
        {
            { SettingKey.Music, SettingsMusic },
            { SettingKey.Sfx, SettingsSfx },
            { SettingKey.Haptics, SettingsHaptics },
            { SettingKey.GuideLine, SettingsGuideLine },
            { SettingKey.ReduceShake, SettingsReduceShake },
            { SettingKey.FingerOffset, SettingsFingerOffset },
            { SettingKey.LeftHanded, SettingsLeftHanded },
        };

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
            (UiTextKeys.CONFIRM_RESTART_TITLE, "RESTART?"),
            (UiTextKeys.CONFIRM_RESTART_MESSAGE, "The current run will be lost."),
            (UiTextKeys.CONFIRM_MENU_TITLE, "LEAVE THE GAME?"),
            (UiTextKeys.CONFIRM_MENU_MESSAGE, "The current run will be lost."),
            (UiTextKeys.CONFIRM_RESET_TITLE, "RESET PROGRESS?"),
            (UiTextKeys.CONFIRM_RESET_MESSAGE, "Your best scores, totals and discovered tiers will be erased. Settings are kept."),
            (UiTextKeys.SETTINGS_ON, "ON"),
            (UiTextKeys.SETTINGS_OFF, "OFF"),
            (SettingsTitle, "SETTINGS"),
            (SettingsMusic, "Music"),
            (SettingsSfx, "Sound effects"),
            (SettingsHaptics, "Haptics"),
            (SettingsGuideLine, "Guide line"),
            (SettingsReduceShake, "Reduce screen shake"),
            (SettingsFingerOffset, "Finger offset"),
            (SettingsLeftHanded, "Left-handed"),
            (SettingsLanguage, "Language"),
            (SettingsLanguageValue, "English"),
            (SettingsReset, "RESET PROGRESS"),
            (SettingsBack, "BACK"),
        };
    }
}
