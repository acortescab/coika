using System;
using UnityEngine;
using UnityEngine.Events;
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
    /// <see cref="Confirmed"/> and <see cref="Cancelled"/>; the Back button is handled by the presenter.
    /// Put it on a panel that starts inactive.
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

        private UnityAction _onConfirmClicked;
        private UnityAction _onCancelClicked;

        /// <summary>Raised once per click on the Confirm button.</summary>
        public event Action Confirmed;

        /// <summary>Raised once per click on the Cancel button.</summary>
        public event Action Cancelled;

        /// <summary>
        /// Caches the click handlers. It runs on the first <see cref="Open"/>, because the panel starts inactive.
        /// </summary>
        private void Awake()
        {
            _onConfirmClicked = HandleConfirmClicked;
            _onCancelClicked = HandleCancelClicked;
        }

        /// <summary>
        /// Starts listening to the buttons while the view is on screen (S-23).
        /// </summary>
        private void OnEnable()
        {
            _confirmButton.onClick.AddListener(_onConfirmClicked);
            _cancelButton.onClick.AddListener(_onCancelClicked);
        }

        /// <summary>
        /// Stops listening when the view is hidden or destroyed (S-23).
        /// </summary>
        private void OnDisable()
        {
            _confirmButton.onClick.RemoveListener(_onConfirmClicked);
            _cancelButton.onClick.RemoveListener(_onCancelClicked);
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
        /// Raises <see cref="Confirmed"/>.
        /// </summary>
        private void HandleConfirmClicked()
        {
            Confirmed?.Invoke();
        }

        /// <summary>
        /// Raises <see cref="Cancelled"/>.
        /// </summary>
        private void HandleCancelClicked()
        {
            Cancelled?.Invoke();
        }
    }
}
