using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// The minimal loading indicator of the Game scene (C-01): a dimmed full-screen panel with a text while the
    /// assets load, and the same panel with an error text and a Retry button when a load fails, so the player never
    /// waits on a spinner that will not end. It is built in code because it must exist before any Addressable asset
    /// does, and its texts are localization keys (S-94).
    /// <para>
    /// It uses unscaled time only through the UI, which needs none. When the failure comes before the Game canvas
    /// (which carries the EventSystem) exists, it makes its own EventSystem so the button can be clicked, and
    /// removes it again when it is hidden.
    /// </para>
    /// </summary>
    public sealed class LoadingOverlay : IDisposable
    {
        /// <summary>Key of the text shown while loading.</summary>
        public const string LOADING_KEY = "loading.text";

        /// <summary>Key of the text shown when a load failed.</summary>
        public const string FAILED_KEY = "loading.failed";

        /// <summary>Key of the label of the retry button.</summary>
        public const string RETRY_KEY = "loading.retry";

        private const int SORTING_ORDER = 100;
        private const float REFERENCE_WIDTH = 1080f;
        private const float REFERENCE_HEIGHT = 1920f;
        private const float BUTTON_WIDTH = 480f;
        private const float BUTTON_HEIGHT = 140f;
        private const float BUTTON_OFFSET = -160f;
        private const float FONT_SIZE = 64f;
        private const float DIM_ALPHA = 0.85f;

        private readonly GameObject _root;
        private readonly LocalizeStringEvent _message;
        private readonly Button _retryButton;
        private readonly UnityAction _onRetryClicked;

        private GameObject _eventSystem;

        /// <summary>
        /// Builds the overlay, hidden.
        /// </summary>
        public LoadingOverlay()
        {
            _root = new GameObject("LoadingOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = CreateChild("Dim", typeof(Image));
            Stretch(dim);
            var dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, DIM_ALPHA);
            dimImage.raycastTarget = true;

            var message = CreateChild("Message", typeof(TextMeshProUGUI), typeof(LocalizeStringEvent));
            Stretch(message);
            var messageText = message.GetComponent<TextMeshProUGUI>();
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.fontSize = FONT_SIZE;
            messageText.raycastTarget = false;
            _message = message.GetComponent<LocalizeStringEvent>();

            var button = CreateChild("RetryButton", typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);
            buttonRect.anchoredPosition = new Vector2(0f, BUTTON_OFFSET);
            _retryButton = button.GetComponent<Button>();

            var label = CreateChild("Label", typeof(TextMeshProUGUI), typeof(LocalizeStringEvent));
            label.transform.SetParent(button.transform, false);
            Stretch(label);
            var labelText = label.GetComponent<TextMeshProUGUI>();
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = FONT_SIZE;
            labelText.color = Color.black;
            labelText.raycastTarget = false;
            label.GetComponent<LocalizeStringEvent>().StringReference = new LocalizedString(UiTextKeys.TABLE_NAME, RETRY_KEY);
            labelText.text = string.Empty;

            _onRetryClicked = HandleRetryClicked;
            _retryButton.onClick.AddListener(_onRetryClicked);

            // The text of a localized label arrives through its event; TMP needs a target for it.
            WireTarget(_message, messageText);
            WireTarget(label.GetComponent<LocalizeStringEvent>(), labelText);

            _root.SetActive(false);
        }

        /// <summary>Raised when the player clicks Retry after a failed load.</summary>
        public event Action RetryClicked;

        /// <summary>Whether the overlay is on screen.</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>Whether the overlay is showing the failure and the Retry button.</summary>
        public bool IsShowingFailure => IsVisible && _retryButton.gameObject.activeSelf;

        /// <summary>
        /// Shows the loading text, with no button.
        /// </summary>
        public void ShowLoading()
        {
            Show(LOADING_KEY, false);
        }

        /// <summary>
        /// Shows the failure text and the Retry button.
        /// </summary>
        public void ShowFailure()
        {
            Show(FAILED_KEY, true);
            EnsureEventSystem();
        }

        /// <summary>
        /// Hides the overlay and removes the EventSystem it made, if any.
        /// </summary>
        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }

            if (_eventSystem != null)
            {
                UnityEngine.Object.Destroy(_eventSystem);
                _eventSystem = null;
            }
        }

        /// <summary>
        /// Unsubscribes from the button and destroys the overlay.
        /// </summary>
        public void Dispose()
        {
            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveListener(_onRetryClicked);
            }

            Hide();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
        }

        /// <summary>
        /// Points the message at a key and shows or hides the button.
        /// </summary>
        private void Show(string key, bool withButton)
        {
            _message.StringReference = new LocalizedString(UiTextKeys.TABLE_NAME, key);
            _retryButton.gameObject.SetActive(withButton);
            _root.SetActive(true);
        }

        /// <summary>
        /// Makes an EventSystem for the button when the scene has none yet.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null || _eventSystem != null)
            {
                return;
            }

            _eventSystem = new GameObject("LoadingEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            _eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        /// <summary>
        /// Raises <see cref="RetryClicked"/> for a click on the button.
        /// </summary>
        private void HandleRetryClicked()
        {
            RetryClicked?.Invoke();
        }

        /// <summary>
        /// Creates a UI child of the root with the given components.
        /// </summary>
        private GameObject CreateChild(string name, params Type[] components)
        {
            var child = new GameObject(name, components);
            if (child.GetComponent<RectTransform>() == null)
            {
                child.AddComponent<RectTransform>();
            }

            child.transform.SetParent(_root.transform, false);
            return child;
        }

        /// <summary>
        /// Makes a rect fill its parent.
        /// </summary>
        private static void Stretch(GameObject target)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Makes a localized string event write into a TMP text.
        /// </summary>
        private static void WireTarget(LocalizeStringEvent localize, TMP_Text text)
        {
            localize.OnUpdateString.AddListener(value => text.text = value);
        }
    }
}
