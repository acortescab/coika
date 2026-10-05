using Coika.Core;

namespace Coika.UI
{
    /// <summary>
    /// The settings the Settings screen has a row for, in screen order: sliders first, then toggles. It is the one
    /// list the prefab builder and the tests read, so a new setting is added here, to the prefab through the builder
    /// and to <see cref="SettingsService"/> (<c>GetFloat</c> or <c>GetBool</c>), and nowhere else.
    /// </summary>
    public static class SettingsRows
    {
        /// <summary>The volume sliders.</summary>
        public static readonly SettingKey[] Sliders =
        {
            SettingKey.Music,
            SettingKey.Sfx,
        };

        /// <summary>The on/off toggles.</summary>
        public static readonly SettingKey[] Toggles =
        {
            SettingKey.Haptics,
            SettingKey.GuideLine,
            SettingKey.ReduceShake,
            SettingKey.FingerOffset,
            SettingKey.LeftHanded,
        };
    }
}
