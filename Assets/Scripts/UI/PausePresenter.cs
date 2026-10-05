using System;
using Coika.Core;

namespace Coika.UI
{
    /// <summary>
    /// Drives the pause flow: it opens the <see cref="PauseView"/> on a <see cref="PanelStack"/>, puts the shared
    /// <see cref="ConfirmView"/> over it for Restart and Menu, and decides what the Back button does. It changes no
    /// game state and loads no scene: the owner listens to <see cref="ResumeRequested"/>,
    /// <see cref="RestartConfirmed"/>, <see cref="MenuConfirmed"/> and <see cref="SettingsRequested"/>, and opens
    /// and closes the presenter from the game state, so the panels always match it. The UI clicks (<c>UiClick</c>,
    /// <c>UiBack</c>) are played here, because the views hold no audio.
    /// </summary>
    public sealed class PausePresenter : IDisposable
    {
        private readonly PauseView _pause;
        private readonly ConfirmView _confirm;
        private readonly IAudioService _audio;
        private readonly PanelStack _stack = new PanelStack();
        private readonly Action _onResumeClicked;
        private readonly Action _onRestartClicked;
        private readonly Action _onSettingsClicked;
        private readonly Action _onMenuClicked;
        private readonly Action _onConfirmed;
        private readonly Action _onCancelled;
        private readonly Action _raiseRestartConfirmed;
        private readonly Action _raiseMenuConfirmed;

        private Action _pendingConfirm;

        /// <summary>
        /// Creates the presenter and subscribes to the views.
        /// </summary>
        /// <param name="pause">The pause menu.</param>
        /// <param name="confirm">The confirmation dialog.</param>
        /// <param name="audio">Plays the UI sounds; null for no sound.</param>
        /// <exception cref="ArgumentNullException">A view is null.</exception>
        public PausePresenter(PauseView pause, ConfirmView confirm, IAudioService audio)
        {
            _pause = pause != null ? pause : throw new ArgumentNullException(nameof(pause));
            _confirm = confirm != null ? confirm : throw new ArgumentNullException(nameof(confirm));
            _audio = audio;

            _onResumeClicked = HandleResumeClicked;
            _onRestartClicked = HandleRestartClicked;
            _onSettingsClicked = HandleSettingsClicked;
            _onMenuClicked = HandleMenuClicked;
            _onConfirmed = HandleConfirmed;
            _onCancelled = HandleCancelled;
            _raiseRestartConfirmed = RaiseRestartConfirmed;
            _raiseMenuConfirmed = RaiseMenuConfirmed;

            _pause.ResumeClicked += _onResumeClicked;
            _pause.RestartClicked += _onRestartClicked;
            _pause.SettingsClicked += _onSettingsClicked;
            _pause.MenuClicked += _onMenuClicked;
            _confirm.Confirmed += _onConfirmed;
            _confirm.Cancelled += _onCancelled;
        }

        /// <summary>Raised when the player asks to go back to the run.</summary>
        public event Action ResumeRequested;

        /// <summary>Raised when the player confirmed the Restart dialog.</summary>
        public event Action RestartConfirmed;

        /// <summary>Raised when the player confirmed the Menu dialog.</summary>
        public event Action MenuConfirmed;

        /// <summary>Raised when the player asks for the Settings screen.</summary>
        public event Action SettingsRequested;

        /// <summary>Whether the pause menu, or a panel over it, is open.</summary>
        public bool IsOpen => _stack.Count > 0;

        /// <summary>Whether a confirmation dialog is on top.</summary>
        public bool IsConfirming => _stack.Top == (IPanel)_confirm;

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
            _pendingConfirm = null;
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
                _pendingConfirm = null;
                _stack.Pop();
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
            _pause.ResumeClicked -= _onResumeClicked;
            _pause.RestartClicked -= _onRestartClicked;
            _pause.SettingsClicked -= _onSettingsClicked;
            _pause.MenuClicked -= _onMenuClicked;
            _confirm.Confirmed -= _onConfirmed;
            _confirm.Cancelled -= _onCancelled;
        }

        /// <summary>
        /// Puts the confirmation dialog over the open panels. What happens on Confirm is the callback.
        /// </summary>
        /// <param name="request">What the dialog asks.</param>
        /// <param name="onConfirmed">Runs when the player confirms.</param>
        private void Confirm(ConfirmRequest request, Action onConfirmed)
        {
            _pendingConfirm = onConfirmed;
            _confirm.Configure(request);
            _stack.Push(_confirm);
        }

        /// <summary>
        /// Plays a UI sound, if there is an audio service.
        /// </summary>
        private void PlaySfx(SfxId id)
        {
            _audio?.PlaySfx(id);
        }

        /// <summary>
        /// Resume button: asks to go back to the run.
        /// </summary>
        private void HandleResumeClicked()
        {
            PlaySfx(SfxId.UiClick);
            ResumeRequested?.Invoke();
        }

        /// <summary>
        /// Restart button: asks for confirmation first.
        /// </summary>
        private void HandleRestartClicked()
        {
            PlaySfx(SfxId.UiClick);
            Confirm(ConfirmRequest.Restart, _raiseRestartConfirmed);
        }

        /// <summary>
        /// Settings button: asks for the Settings screen.
        /// </summary>
        private void HandleSettingsClicked()
        {
            PlaySfx(SfxId.UiClick);
            SettingsRequested?.Invoke();
        }

        /// <summary>
        /// Menu button: asks for confirmation first.
        /// </summary>
        private void HandleMenuClicked()
        {
            PlaySfx(SfxId.UiClick);
            Confirm(ConfirmRequest.Menu, _raiseMenuConfirmed);
        }

        /// <summary>
        /// Confirm button of the dialog: closes the dialog and runs what it asked.
        /// </summary>
        private void HandleConfirmed()
        {
            PlaySfx(SfxId.UiClick);
            var action = _pendingConfirm;
            _pendingConfirm = null;
            _stack.Pop();
            action?.Invoke();
        }

        /// <summary>
        /// Cancel button of the dialog: closes it and changes nothing.
        /// </summary>
        private void HandleCancelled()
        {
            PlaySfx(SfxId.UiBack);
            _pendingConfirm = null;
            _stack.Pop();
        }

        /// <summary>
        /// Raises <see cref="RestartConfirmed"/>.
        /// </summary>
        private void RaiseRestartConfirmed()
        {
            RestartConfirmed?.Invoke();
        }

        /// <summary>
        /// Raises <see cref="MenuConfirmed"/>.
        /// </summary>
        private void RaiseMenuConfirmed()
        {
            MenuConfirmed?.Invoke();
        }
    }
}
