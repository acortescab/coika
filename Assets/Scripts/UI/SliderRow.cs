using System;
using Coika.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// One slider of the <see cref="SettingsView"/> and the setting it edits. A new slider is a new row in the
    /// prefab, not new code in the view.
    /// </summary>
    [Serializable]
    public sealed class SliderRow
    {
        [SerializeField]
        private SettingKey _key;
        [SerializeField]
        private Slider _slider;

        /// <summary>
        /// Creates a row.
        /// </summary>
        /// <param name="key">The setting the slider edits.</param>
        /// <param name="slider">The slider, from 0 to 1.</param>
        public SliderRow(SettingKey key, Slider slider)
        {
            _key = key;
            _slider = slider;
        }

        /// <summary>The setting the slider edits.</summary>
        public SettingKey Key => _key;

        /// <summary>The slider, from 0 to 1.</summary>
        public Slider Slider => _slider;
    }
}
