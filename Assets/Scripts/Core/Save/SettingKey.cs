namespace Coika.Core
{
    /// <summary>
    /// Identifies a user setting in a <see cref="SettingsChanged"/> event.
    /// </summary>
    public enum SettingKey
    {
        /// <summary>Master volume.</summary>
        Master,

        /// <summary>Music volume.</summary>
        Music,

        /// <summary>Sound effects volume.</summary>
        Sfx,

        /// <summary>Haptic feedback.</summary>
        Haptics,

        /// <summary>Drop guide line.</summary>
        GuideLine,

        /// <summary>Reduced screen shake.</summary>
        ReduceShake,

        /// <summary>Left-handed layout.</summary>
        LeftHanded,

        /// <summary>Language code.</summary>
        Language,
    }
}
