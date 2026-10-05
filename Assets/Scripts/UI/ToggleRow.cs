using System;
using Coika.Core;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// One toggle of the <see cref="SettingsView"/>: the setting it edits, the switch, and a text that says "ON"
    /// or "OFF" so the state is never only a colour. A new toggle is a new row in the prefab, not new code in the
    /// view.
    /// </summary>
    [Serializable]
    public sealed class ToggleRow
    {
        [SerializeField]
        private SettingKey _key;
        [SerializeField]
        private Toggle _toggle;
        [SerializeField]
        private LocalizeStringEvent _stateLabel;

        /// <summary>
        /// Creates a row.
        /// </summary>
        /// <param name="key">The setting the toggle edits.</param>
        /// <param name="toggle">The switch.</param>
        /// <param name="stateLabel">The text that shows the state.</param>
        public ToggleRow(SettingKey key, Toggle toggle, LocalizeStringEvent stateLabel)
        {
            _key = key;
            _toggle = toggle;
            _stateLabel = stateLabel;
        }

        /// <summary>The setting the toggle edits.</summary>
        public SettingKey Key => _key;

        /// <summary>The switch.</summary>
        public Toggle Toggle => _toggle;

        /// <summary>The text that shows the state.</summary>
        public LocalizeStringEvent StateLabel => _stateLabel;
    }
}
