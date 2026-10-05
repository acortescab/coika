using System;
using Coika.Core;

namespace Coika.UI
{
    /// <summary>
    /// Drives the pause flow: it opens the <see cref="PauseView"/> on a <see cref="PanelStack"/>, puts the
    /// <see cref="SettingsView"/> or the shared <see cref="ConfirmView"/> over it, and decides what the Back button
    /// does. It changes no game state and loads no scene: the owner listens to <see cref="ResumeRequested"/>
    /// and <see cref="Confirmed"/>, and opens and closes the presenter from the
    /// game state, so the panels always match it. The UI clicks (<c>UiClick</c>, <c>UiBack</c>) are played here,
    /// because the views hold no audio.
    /// </summary>
    public sealed class PausePresenter : IDisposable
    {
        private readonly PauseView _pause;
        private readonly ConfirmView _confirm;
        private readonly SettingsView _settings;
        private readonly IAudioService _audio;
        private readonly PanelStack _stack = new PanelStack();
        private readonly Action<PauseAction> _onPauseClicked;
        private readonly Action<bool> _onAnswered;
        private readonly Action<SettingsAction> _onSettingsClicked;

        private ConfirmRequest? _pending;

        /// <summary>
        /// Creates the presenter and subscribes to the views.
        /// </summary>
        /// <param name="pause">The pause menu.</param>
        /// <param name="confirm">The confirmation dialog.</param>
        /// <param name="settings">The Settings screen.</param>
        /// <param name="audio">Plays the UI sounds; null for no sound.</param>
        /// <exception cref="ArgumentNullException">A view is null.</exception>
        public PausePresenter(PauseView pause, ConfirmView confirm, SettingsView settings, IAudioService audio)
        {
            _pause = pause != null ? pause : throw new ArgumentNullException(nameof(pause));
            _confirm = confirm != null ? confirm : throw new ArgumentNullException(nameof(confirm));
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _audio = audio;

            _onPauseClicked = HandlePauseClicked;
            _onAnswered = HandleAnswered;
            _onSettingsClicked = HandleSettingsClicked;
            _pause.Clicked += _onPauseClicked;
            _confirm.Answered += _onAnswered;
            _settings.Clicked += _onSettingsClicked;
        }

        /// <summary>Raised when the player asks to go back to the run.</summary>
        public event Action ResumeRequested;

        /// <summary>Raised when the player confirmed a dialog, with what it asked.</summary>
        public event Action<ConfirmKind> Confirmed;

        /// <summary>Whether the pause menu, or a panel over it, is open.</summary>
        public bool IsOpen => _stack.Count > 0;

        /// <summary>Whether a confirmation dialog is on top.</summary>
        public bool IsConfirming => _stack.Top == (IPanel)_confirm;

        /// <summary>Whether the Settings screen is on top.</summary>
        public bool IsInSettings => _stack.Top == (IPanel)_settings;

        /// <summary>
        /// Opens the pause menu. Does nothing when it is already open.
        /// </summary>
        public void Open()
        {
            if (!IsOpen)
            {
                _stack.Push(_pause);
            }
        }

        /// <summary>
        /// Closes every panel and forgets any pending confirmation. Does nothing when nothing is open.
        /// </summary>
        public void Close()
        {
            _pending = null;
            _stack.Clear();
        }

        /// <summary>
        /// Handles the Back button while the pause flow is open: it closes the top panel (a dialog counts as a
        /// cancel), and on the pause menu itself it asks to resume.
        /// </summary>
        /// <returns>True when the press was used, false when nothing is open.</returns>
        public bool HandleBack()
        {
            if (!IsOpen)
            {
                return false;
            }

            PlaySfx(SfxId.UiBack);
            if (_stack.Count > 1)
            {
                PopTop();
            }
            else
            {
                ResumeRequested?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// Unsubscribes from the views. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            _pause.Clicked -= _onPauseClicked;
            _confirm.Answered -= _onAnswered;
            _settings.Clicked -= _onSettingsClicked;
        }

        /// <summary>
        /// Puts the confirmation dialog over the open panels.
        /// </summary>
        /// <param name="request">What the dialog asks.</param>
        private void Ask(ConfirmRequest request)
        {
            _pending = request;
            _confirm.Configure(request);
            _stack.Push(_confirm);
        }

        /// <summary>
        /// Closes the panel on top and forgets the confirmation it was asking.
        /// </summary>
        /// <returns>What the closed dialog asked, or null when the top panel was not a dialog.</returns>
        private ConfirmRequest? PopTop()
        {
            var asked = _pending;
            _pending = null;
            _stack.Pop();
            return asked;
        }

        /// <summary>
        /// Plays a UI sound, if there is an audio service.
        /// </summary>
        private void PlaySfx(SfxId id)
        {
            _audio?.PlaySfx(id);
        }

        /// <summary>
        /// A button of the pause menu: Resume is passed on, Settings opens its screen, the others ask for
        /// confirmation first.
        /// </summary>
        /// <param name="action">The button that was clicked.</param>
        private void HandlePauseClicked(PauseAction action)
        {
            PlaySfx(SfxId.UiClick);
            switch (action)
            {
                case PauseAction.Resume:
                    ResumeRequested?.Invoke();
                    break;
                case PauseAction.Settings:
                    _stack.Push(_settings);
                    break;
                case PauseAction.Restart:
                    Ask(ConfirmRequest.Restart);
                    break;
                case PauseAction.Menu:
                    Ask(ConfirmRequest.Menu);
                    break;
            }
        }

        /// <summary>
        /// A button of the Settings screen: Back closes it, Reset progress asks for confirmation first.
        /// </summary>
        /// <param name="action">The button that was clicked.</param>
        private void HandleSettingsClicked(SettingsAction action)
        {
            switch (action)
            {
                case SettingsAction.Back:
                    HandleBack();
                    break;
                case SettingsAction.ResetProgress:
                    PlaySfx(SfxId.UiClick);
                    Ask(ConfirmRequest.ResetProgress);
                    break;
            }
        }

        /// <summary>
        /// The answer of the dialog: it closes, and a Confirm announces what was asked.
        /// </summary>
        /// <param name="confirmed">True for Confirm, false for Cancel.</param>
        private void HandleAnswered(bool confirmed)
        {
            PlaySfx(confirmed ? SfxId.UiClick : SfxId.UiBack);
            var asked = PopTop();
            if (confirmed && asked.HasValue)
            {
                Confirmed?.Invoke(asked.Value.Kind);
            }
        }
    }
}
