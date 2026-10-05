using System;
using Coika.Core;

namespace Coika.UI
{
    /// <summary>
    /// Binds the <see cref="SettingsView"/> to the <see cref="SettingsService"/> and nothing else: it shows the saved
    /// values each time the screen opens and writes every change at once, with no Apply button. The systems that
    /// consume the settings (mixer, haptics, guide line, shake, finger offset) already listen to the service, so
    /// they follow without this class knowing them. Back and Reset progress are not handled here: they belong to
    /// the panel flow, which is the <see cref="PausePresenter"/> today and the Menu in M3.
    /// </summary>
    public sealed class SettingsPresenter : IDisposable
    {
        private readonly SettingsView _view;
        private readonly SettingsService _settings;
        private readonly IAudioService _audio;
        private readonly Action _onOpened;
        private readonly Action<SettingKey, float> _onSliderChanged;
        private readonly Action<SettingKey> _onSliderReleased;
        private readonly Action<SettingKey, bool> _onToggleChanged;

        /// <summary>
        /// Creates the presenter and subscribes to the view.
        /// </summary>
        /// <param name="view">The Settings screen.</param>
        /// <param name="settings">Where the values are read from and written to.</param>
        /// <param name="audio">Plays the SFX volume preview and the toggle click; null for no sound.</param>
        /// <exception cref="ArgumentNullException">The view or the settings are null.</exception>
        public SettingsPresenter(SettingsView view, SettingsService settings, IAudioService audio)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _audio = audio;

            _onOpened = Refresh;
            _onSliderChanged = HandleSliderChanged;
            _onSliderReleased = HandleSliderReleased;
            _onToggleChanged = HandleToggleChanged;
            _view.Opened += _onOpened;
            _view.SliderChanged += _onSliderChanged;
            _view.SliderReleased += _onSliderReleased;
            _view.ToggleChanged += _onToggleChanged;
        }

        /// <summary>
        /// Shows the saved value of every control.
        /// </summary>
        public void Refresh()
        {
            foreach (var key in _view.SliderKeys)
            {
                _view.SetSlider(key, _settings.GetFloat(key));
            }

            foreach (var key in _view.ToggleKeys)
            {
                _view.SetToggle(key, _settings.GetBool(key));
            }
        }

        /// <summary>
        /// Unsubscribes from the view. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            _view.Opened -= _onOpened;
            _view.SliderChanged -= _onSliderChanged;
            _view.SliderReleased -= _onSliderReleased;
            _view.ToggleChanged -= _onToggleChanged;
        }

        /// <summary>
        /// Writes the volume the player dragged to.
        /// </summary>
        /// <param name="key">The volume setting.</param>
        /// <param name="value">The new value.</param>
        private void HandleSliderChanged(SettingKey key, float value)
        {
            _settings.SetFloat(key, value);
        }

        /// <summary>
        /// Plays a sound at the new volume when the SFX slider is released, so the player hears it. The music is
        /// already audible while it is dragged.
        /// </summary>
        /// <param name="key">The volume setting.</param>
        private void HandleSliderReleased(SettingKey key)
        {
            if (key == SettingKey.Sfx)
            {
                _audio?.PlaySfx(SfxId.UiClick);
            }
        }

        /// <summary>
        /// Writes the state the player switched to.
        /// </summary>
        /// <param name="key">The toggle setting.</param>
        /// <param name="isOn">The new state.</param>
        private void HandleToggleChanged(SettingKey key, bool isOn)
        {
            _settings.SetBool(key, isOn);
            _audio?.PlaySfx(SfxId.UiClick);
        }
    }
}
