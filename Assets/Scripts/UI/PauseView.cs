using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// Passive pause menu (GDD §8.3): Resume, Restart, Settings and Menu. It loads no scene and changes no state:
    /// every button only raises an event for the <see cref="PausePresenter"/>. Settings and Menu are disabled
    /// placeholders until the Settings screen (#36) and the Menu scene (M3) exist. It has no animation, so it does
    /// not depend on the time scale, which is 0 while it is open (S-64). Put it on a panel that starts inactive.
    /// </summary>
    [AddComponentMenu("Coika/UI/Pause View")]
    [DisallowMultipleComponent]
    public class PauseView : MonoBehaviour, IPanel
    {
        [SerializeField]
        private Button _resumeButton;
        [SerializeField]
        private Button _restartButton;
        [SerializeField]
        private Button _settingsButton;
        [SerializeField]
        private Button _menuButton;

        private UnityAction _onResumeClicked;
        private UnityAction _onRestartClicked;
        private UnityAction _onSettingsClicked;
        private UnityAction _onMenuClicked;

        /// <summary>Raised once per click on the Resume button.</summary>
        public event Action ResumeClicked;

        /// <summary>Raised once per click on the Restart button.</summary>
        public event Action RestartClicked;

        /// <summary>Raised once per click on the Settings button.</summary>
        public event Action SettingsClicked;

        /// <summary>Raised once per click on the Menu button.</summary>
        public event Action MenuClicked;

        /// <summary>
        /// Caches the click handlers and disables the placeholders. It runs on the first <see cref="Open"/>, because
        /// the panel starts inactive.
        /// </summary>
        private void Awake()
        {
            _onResumeClicked = HandleResumeClicked;
            _onRestartClicked = HandleRestartClicked;
            _onSettingsClicked = HandleSettingsClicked;
            _onMenuClicked = HandleMenuClicked;
            _settingsButton.interactable = false;
            _menuButton.interactable = false;
        }

        /// <summary>
        /// Starts listening to the buttons while the view is on screen (S-23).
        /// </summary>
        private void OnEnable()
        {
            _resumeButton.onClick.AddListener(_onResumeClicked);
            _restartButton.onClick.AddListener(_onRestartClicked);
            _settingsButton.onClick.AddListener(_onSettingsClicked);
            _menuButton.onClick.AddListener(_onMenuClicked);
        }

        /// <summary>
        /// Stops listening when the view is hidden or destroyed (S-23).
        /// </summary>
        private void OnDisable()
        {
            _resumeButton.onClick.RemoveListener(_onResumeClicked);
            _restartButton.onClick.RemoveListener(_onRestartClicked);
            _settingsButton.onClick.RemoveListener(_onSettingsClicked);
            _menuButton.onClick.RemoveListener(_onMenuClicked);
        }

        /// <inheritdoc />
        public void Open()
        {
            gameObject.SetActive(true);
        }

        /// <inheritdoc />
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Raises <see cref="ResumeClicked"/>.
        /// </summary>
        private void HandleResumeClicked()
        {
            ResumeClicked?.Invoke();
        }

        /// <summary>
        /// Raises <see cref="RestartClicked"/>.
        /// </summary>
        private void HandleRestartClicked()
        {
            RestartClicked?.Invoke();
        }

        /// <summary>
        /// Raises <see cref="SettingsClicked"/>.
        /// </summary>
        private void HandleSettingsClicked()
        {
            SettingsClicked?.Invoke();
        }

        /// <summary>
        /// Raises <see cref="MenuClicked"/>.
        /// </summary>
        private void HandleMenuClicked()
        {
            MenuClicked?.Invoke();
        }
    }
}
