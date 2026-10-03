using System;
using Coika.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// Passive Game Over view (GDD §8.4): final score, best score, a "NEW BEST!" banner when it applies, the highest
    /// tier reached, the pieces dropped and the play time, with Retry and Menu buttons. It loads no scene and starts
    /// no run: Retry only raises <see cref="RetryClicked"/> for the game manager (issue #11). Menu is a disabled
    /// placeholder until there is a menu scene.
    /// <para>
    /// It fades in over <see cref="FADE_SECONDS"/> on unscaled time (S-64), because the game freezes when the run
    /// ends. Put it on a panel that starts inactive; <see cref="Show"/> activates it.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/UI/Game Over View")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class GameOverView : MonoBehaviour
    {
        /// <summary>Duration of the fade-in, in seconds.</summary>
        public const float FADE_SECONDS = 0.2f;

        private const string NUMBER_FORMAT = "{0}";
        private const string TIME_FORMAT = "{0}:{1}{2}";
        private const int SECONDS_PER_MINUTE = 60;
        private const int SECONDS_PER_TEN = 10;

        [SerializeField]
        private TMP_Text _scoreText;
        [SerializeField]
        private TMP_Text _bestText;
        [SerializeField]
        private GameObject _newBestBanner;
        [SerializeField]
        private Image _highestTierIcon;
        [SerializeField]
        private TMP_Text _piecesText;
        [SerializeField]
        private TMP_Text _timeText;
        [SerializeField]
        private Button _retryButton;
        [SerializeField]
        private Button _menuButton;

        private CanvasGroup _group;
        private UnityAction _onRetryClicked;
        private float _fadeElapsed;
        private bool _initialized;

        /// <summary>Raised once per click on the Retry button.</summary>
        public event Action RetryClicked;

        /// <summary>Whether the view is on screen.</summary>
        public bool IsShown => gameObject.activeSelf;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _onRetryClicked = HandleRetryClicked;
            _retryButton.onClick.AddListener(_onRetryClicked);
            _menuButton.interactable = false;
            _initialized = true;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (_initialized && _retryButton != null)
            {
                _retryButton.onClick.RemoveListener(_onRetryClicked);
            }
        }

        private void Update()
        {
            _fadeElapsed += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(_fadeElapsed / FADE_SECONDS);
            if (_fadeElapsed >= FADE_SECONDS)
            {
                enabled = false;
            }
        }

        /// <summary>
        /// Fills the view from the summary and shows it with a fade-in. The highest-tier icon is set separately
        /// with <see cref="SetHighestTierIcon"/>, because the view loads no assets.
        /// </summary>
        /// <param name="summary">Snapshot of the finished run.</param>
        public void Show(RunSummary summary)
        {
            gameObject.SetActive(true);

            _scoreText.SetText(NUMBER_FORMAT, summary.Score);
            _bestText.SetText(NUMBER_FORMAT, summary.BestScore);
            _newBestBanner.SetActive(summary.IsNewBest);
            _piecesText.SetText(NUMBER_FORMAT, summary.PiecesDropped);

            var totalSeconds = Mathf.FloorToInt(summary.DurationSeconds);
            // The seconds are two digits (tens, units) so the padding does not depend on TMP number-format rules.
            var seconds = totalSeconds % SECONDS_PER_MINUTE;
            _timeText.SetText(TIME_FORMAT, totalSeconds / SECONDS_PER_MINUTE, seconds / SECONDS_PER_TEN, seconds % SECONDS_PER_TEN);

            _fadeElapsed = 0f;
            _group.alpha = 0f;
            enabled = true;
        }

        /// <summary>
        /// Hides the view at once.
        /// </summary>
        public void Hide()
        {
            enabled = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Sets the icon of the highest tier reached.
        /// </summary>
        /// <param name="sprite">Sprite of that tier; null hides the icon.</param>
        public void SetHighestTierIcon(Sprite sprite)
        {
            _highestTierIcon.sprite = sprite;
            _highestTierIcon.enabled = sprite != null;
        }

        private void HandleRetryClicked()
        {
            RetryClicked?.Invoke();
        }
    }
}
