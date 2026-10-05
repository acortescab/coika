using System;
using System.Collections.Generic;
using Coika.Core;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// Passive Settings screen (GDD §8.5): sliders, toggles, a disabled Language row, Reset progress and Back. It
    /// holds no setting: the <see cref="SettingsPresenter"/> shows the saved values with <see cref="SetSlider"/> and
    /// <see cref="SetToggle"/> (which raise nothing) and writes what the events report. The rows are data, a
    /// <see cref="SliderRow"/> or a <see cref="ToggleRow"/> per setting, so a new setting is a new row in the prefab.
    /// It has no animation, so it does not depend on the time scale, which is 0 while the pause menu under it is
    /// open (S-64). Put it on a panel that starts inactive; the Pause menu and, in M3, the Menu push the same prefab.
    /// </summary>
    [AddComponentMenu("Coika/UI/Settings View")]
    [DisallowMultipleComponent]
    public class SettingsView : MonoBehaviour, IPanel
    {
        [SerializeField]
        private SliderRow[] _sliders;
        [SerializeField]
        private ToggleRow[] _toggles;
        [SerializeField]
        private Button _resetButton;
        [SerializeField]
        private Button _backButton;

        private readonly Dictionary<SettingKey, Slider> _sliderByKey = new Dictionary<SettingKey, Slider>();
        private readonly Dictionary<SettingKey, ToggleRow> _toggleByKey = new Dictionary<SettingKey, ToggleRow>();
        private readonly List<IRelay> _relays = new List<IRelay>();
        private SettingKey[] _sliderKeys;
        private SettingKey[] _toggleKeys;
        private LocalizedString _on;
        private LocalizedString _off;

        /// <summary>Raised on every step of a slider, with the setting and the new value.</summary>
        public event Action<SettingKey, float> SliderChanged;

        /// <summary>Raised when the pointer is lifted from a slider, with the setting.</summary>
        public event Action<SettingKey> SliderReleased;

        /// <summary>Raised when a toggle is switched by the player, with the setting and the new state.</summary>
        public event Action<SettingKey, bool> ToggleChanged;

        /// <summary>Raised on a click of Back or Reset progress.</summary>
        public event Action<SettingsAction> Clicked;

        /// <summary>Raised every time the screen opens, after it is active: the values must be shown again.</summary>
        public event Action Opened;

        /// <summary>The settings that have a slider.</summary>
        public ReadOnlySpan<SettingKey> SliderKeys => _sliderKeys;

        /// <summary>The settings that have a toggle.</summary>
        public ReadOnlySpan<SettingKey> ToggleKeys => _toggleKeys;

        /// <summary>
        /// Makes one relay per control and the two state texts. It runs on the first <see cref="Open"/>, because the
        /// panel starts inactive.
        /// </summary>
        private void Awake()
        {
            _on = new LocalizedString(UiTextKeys.TABLE_NAME, UiTextKeys.SETTINGS_ON);
            _off = new LocalizedString(UiTextKeys.TABLE_NAME, UiTextKeys.SETTINGS_OFF);

            _sliderKeys = new SettingKey[_sliders.Length];
            for (var i = 0; i < _sliders.Length; i++)
            {
                var row = _sliders[i];
                _sliderKeys[i] = row.Key;
                _sliderByKey.Add(row.Key, row.Slider);
                _relays.Add(new KeyedRelay<float>(row.Slider.onValueChanged, row.Key, RaiseSliderChanged));
                _relays.Add(new ReleaseRelay(row.Slider, row.Key, RaiseSliderReleased));
            }

            _toggleKeys = new SettingKey[_toggles.Length];
            for (var i = 0; i < _toggles.Length; i++)
            {
                var row = _toggles[i];
                _toggleKeys[i] = row.Key;
                _toggleByKey.Add(row.Key, row);
                _relays.Add(new KeyedRelay<bool>(row.Toggle.onValueChanged, row.Key, RaiseToggleChanged));
            }

            _relays.Add(new ButtonRelay<SettingsAction>(_resetButton, SettingsAction.ResetProgress, RaiseClicked));
            _relays.Add(new ButtonRelay<SettingsAction>(_backButton, SettingsAction.Back, RaiseClicked));
        }

        /// <summary>
        /// Starts listening to the controls while the view is on screen (S-23).
        /// </summary>
        private void OnEnable()
        {
            foreach (var relay in _relays)
            {
                relay.Bind();
            }
        }

        /// <summary>
        /// Stops listening when the view is hidden or destroyed (S-23).
        /// </summary>
        private void OnDisable()
        {
            foreach (var relay in _relays)
            {
                relay.Unbind();
            }
        }

        /// <inheritdoc />
        public void Open()
        {
            gameObject.SetActive(true);
            Opened?.Invoke();
        }

        /// <inheritdoc />
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the value of a slider without raising <see cref="SliderChanged"/>.
        /// </summary>
        /// <param name="key">The setting.</param>
        /// <param name="value">Its value, from 0 to 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">The setting has no slider.</exception>
        public void SetSlider(SettingKey key, float value)
        {
            if (!_sliderByKey.TryGetValue(key, out var slider))
            {
                throw new ArgumentOutOfRangeException(nameof(key), key, "The setting has no slider.");
            }

            slider.SetValueWithoutNotify(value);
        }

        /// <summary>
        /// Shows the state of a toggle, and its ON or OFF text, without raising <see cref="ToggleChanged"/>.
        /// </summary>
        /// <param name="key">The setting.</param>
        /// <param name="isOn">Its state.</param>
        /// <exception cref="ArgumentOutOfRangeException">The setting has no toggle.</exception>
        public void SetToggle(SettingKey key, bool isOn)
        {
            if (!_toggleByKey.TryGetValue(key, out var row))
            {
                throw new ArgumentOutOfRangeException(nameof(key), key, "The setting has no toggle.");
            }

            row.Toggle.SetIsOnWithoutNotify(isOn);
            row.StateLabel.StringReference = isOn ? _on : _off;
        }

        /// <summary>
        /// Raises <see cref="SliderChanged"/>.
        /// </summary>
        /// <param name="key">The setting.</param>
        /// <param name="value">The new value.</param>
        private void RaiseSliderChanged(SettingKey key, float value)
        {
            SliderChanged?.Invoke(key, value);
        }

        /// <summary>
        /// Raises <see cref="SliderReleased"/>.
        /// </summary>
        /// <param name="key">The setting.</param>
        private void RaiseSliderReleased(SettingKey key)
        {
            SliderReleased?.Invoke(key);
        }

        /// <summary>
        /// Updates the ON or OFF text of the toggle and raises <see cref="ToggleChanged"/>.
        /// </summary>
        /// <param name="key">The setting.</param>
        /// <param name="isOn">The new state.</param>
        private void RaiseToggleChanged(SettingKey key, bool isOn)
        {
            SetToggle(key, isOn);
            ToggleChanged?.Invoke(key, isOn);
        }

        /// <summary>
        /// Raises <see cref="Clicked"/>.
        /// </summary>
        /// <param name="action">The button that was clicked.</param>
        private void RaiseClicked(SettingsAction action)
        {
            Clicked?.Invoke(action);
        }
    }
}
