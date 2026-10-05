using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// Reusable confirmation dialog: a title, a message, and Confirm and Cancel buttons. What it asks is a
    /// <see cref="ConfirmRequest"/> set with <see cref="Configure"/>, so Restart, Menu and Reset progress share it.
    /// Cancel is the default: it is the selected button when the dialog opens. The view only raises
    /// <see cref="Answered"/>; the Back button is handled by the presenter. Put it on a panel that starts inactive.
    /// </summary>
    [AddComponentMenu("Coika/UI/Confirm View")]
    [DisallowMultipleComponent]
    public class ConfirmView : MonoBehaviour, IPanel
    {
        [SerializeField]
        private LocalizeStringEvent _title;
        [SerializeField]
        private LocalizeStringEvent _message;
        [SerializeField]
        private Button _confirmButton;
        [SerializeField]
        private Button _cancelButton;

        private ButtonRelay<bool>[] _relays;

        /// <summary>Raised once per click on a button: true for Confirm, false for Cancel.</summary>
        public event Action<bool> Answered;

        /// <summary>
        /// Makes one relay per button. It runs on the first <see cref="Open"/>, because the panel starts inactive.
        /// </summary>
        private void Awake()
        {
            _relays = new[]
            {
                new ButtonRelay<bool>(_confirmButton, true, RaiseAnswered),
                new ButtonRelay<bool>(_cancelButton, false, RaiseAnswered),
            };
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

        /// <summary>
        /// Sets what the dialog asks.
        /// </summary>
        /// <param name="request">Keys of the title and the message.</param>
        public void Configure(ConfirmRequest request)
        {
            _title.StringReference = new LocalizedString(UiTextKeys.TABLE_NAME, request.TitleKey);
            _message.StringReference = new LocalizedString(UiTextKeys.TABLE_NAME, request.MessageKey);
        }

        /// <inheritdoc />
        public void Open()
        {
            gameObject.SetActive(true);

            // Cancel is the default answer, so a stray Enter or gamepad press never confirms.
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_cancelButton.gameObject);
            }
        }

        /// <inheritdoc />
        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Raises <see cref="Answered"/>.
        /// </summary>
        /// <param name="confirmed">True for Confirm, false for Cancel.</param>
        private void RaiseAnswered(bool confirmed)
        {
            Answered?.Invoke(confirmed);
        }
    }
}
