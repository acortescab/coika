using System;
using UnityEngine;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// Passive pause menu (GDD §8.3): Resume, Restart, Settings and Menu. It loads no scene and changes no state:
    /// every button raises <see cref="Clicked"/> with its <see cref="PauseAction"/> for the
    /// <see cref="PausePresenter"/>. Menu is a disabled placeholder until the Menu scene (M3) exists. It has no animation, so it does not depend on the time scale, which is 0
    /// while it is open (S-64). Put it on a panel that starts inactive.
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

        private ButtonRelay<PauseAction>[] _relays;

        /// <summary>Raised once per click on a button, with what the button stands for.</summary>
        public event Action<PauseAction> Clicked;

        /// <summary>
        /// Makes one relay per button and disables the placeholder. It runs on the first <see cref="Open"/>,
        /// because the panel starts inactive.
        /// </summary>
        private void Awake()
        {
            _relays = new[]
            {
                new ButtonRelay<PauseAction>(_resumeButton, PauseAction.Resume, RaiseClicked),
                new ButtonRelay<PauseAction>(_restartButton, PauseAction.Restart, RaiseClicked),
                new ButtonRelay<PauseAction>(_settingsButton, PauseAction.Settings, RaiseClicked),
                new ButtonRelay<PauseAction>(_menuButton, PauseAction.Menu, RaiseClicked),
            };
            _menuButton.interactable = false;
        }

        /// <summary>
        /// Starts listening to the buttons while the view is on screen (S-23).
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
        }

        /// <inheritdoc />
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Raises <see cref="Clicked"/>.
        /// </summary>
        /// <param name="action">The button that was clicked.</param>
        private void RaiseClicked(PauseAction action)
        {
            Clicked?.Invoke(action);
        }
    }
}
