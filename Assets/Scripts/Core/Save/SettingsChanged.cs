namespace Coika.Core
{
    /// <summary>
    /// Describes one setting change. A struct, so raising it allocates nothing.
    /// </summary>
    public readonly struct SettingsChanged
    {
        /// <summary>
        /// Creates the event.
        /// </summary>
        /// <param name="key">Setting that changed.</param>
        /// <param name="number">New value of a volume setting, otherwise 0.</param>
        /// <param name="flag">New value of a toggle setting, otherwise false.</param>
        /// <param name="text">New value of the language setting, otherwise null.</param>
        public SettingsChanged(SettingKey key, float number, bool flag, string text)
        {
            Key = key;
            Number = number;
            Flag = flag;
            Text = text;
        }

        /// <summary>Setting that changed.</summary>
        public SettingKey Key { get; }

        /// <summary>New value of a volume setting, otherwise 0.</summary>
        public float Number { get; }

        /// <summary>New value of a toggle setting, otherwise false.</summary>
        public bool Flag { get; }

        /// <summary>New value of the language setting, otherwise null.</summary>
        public string Text { get; }
    }
}
