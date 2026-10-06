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
        private const string TIME_FORMAT = "{0}{1}:{2}{3}";
        private const int SECONDS_PER_MINUTE = 60;
        private const int DIGIT_BASE = 10;

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
        private Func<double> _clock = UnscaledNow;
        private CountUp _scoreCount;
        private float _motionScale = 1f;
        private double _shownAt;
        private float _fadeElapsed;
        private bool _fading;
        private bool _retryReady;
        private bool _retryConsumed;

        /// <summary>Raised once per click on the Retry button.</summary>
        public event Action RetryClicked;

        /// <summary>
        /// Caches the canvas group and the click handler, and disables the Menu placeholder. It runs on the first
        /// <see cref="Show"/>, because the panel starts inactive.
        /// </summary>
        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _onRetryClicked = HandleRetryClicked;
            _menuButton.interactable = false;
        }

        /// <summary>
        /// Starts listening to the Retry button while the view is on screen (S-23).
        /// </summary>
        private void OnEnable()
        {
            _retryButton.onClick.AddListener(_onRetryClicked);
        }

        /// <summary>
        /// Stops listening to the Retry button when the view is hidden or destroyed (S-23).
        /// </summary>
        private void OnDisable()
        {
            _retryButton.onClick.RemoveListener(_onRetryClicked);
        }

        /// <summary>
        /// Gives the view the clock of its animations. Without it the view uses the real unscaled time.
        /// </summary>
        /// <param name="clock">Unscaled time source in seconds.</param>
        /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
        public void Initialize(Func<double> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// Scales the count-up and the Retry lock, for reduced motion (<see cref="UiAnimation.MotionScale"/>).
        /// </summary>
        /// <param name="scale">1 for normal motion, less to shorten them.</param>
        public void SetMotionScale(float scale)
        {
            _motionScale = scale;
        }

        /// <summary>
        /// Runs the fade-in, the score count-up and the Retry lock on unscaled time, so they play even when the
        /// game is frozen (S-64).
        /// </summary>
        private void Update()
        {
            if (_fading)
            {
                _fadeElapsed += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Clamp01(_fadeElapsed / FADE_SECONDS);
                _fading = _fadeElapsed < FADE_SECONDS;
            }

            if (_scoreCount != null && _scoreCount.Tick())
            {
                _scoreText.SetText(NUMBER_FORMAT, _scoreCount.Value);
            }

            if (!_retryReady && _clock() - _shownAt >= UiAnimation.RETRY_LOCK_SECONDS * _motionScale)
            {
                _retryReady = true;
                _retryButton.interactable = true;
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

            // The score rolls up from 0 to the exact final value.
            _scoreCount = new CountUp(_clock, UiAnimation.COUNT_UP_SECONDS);
            _scoreCount.SetScale(_motionScale);
            _scoreText.SetText(NUMBER_FORMAT, 0);
            _scoreCount.SetTarget(summary.Score);
            if (!_scoreCount.Running)
            {
                _scoreText.SetText(NUMBER_FORMAT, summary.Score);
            }

            _bestText.SetText(NUMBER_FORMAT, summary.BestScore);
            _newBestBanner.SetActive(summary.IsNewBest);
            _piecesText.SetText(NUMBER_FORMAT, summary.PiecesDropped);

            // Minutes and seconds go in as separate digits, so the two-digit padding (mm:ss) does not depend on
            // TMP number-format rules. Minutes past 99 simply grow the tens digit.
            var totalSeconds = Mathf.FloorToInt(summary.DurationSeconds);
            var minutes = totalSeconds / SECONDS_PER_MINUTE;
            var seconds = totalSeconds % SECONDS_PER_MINUTE;
            _timeText.SetText(TIME_FORMAT, minutes / DIGIT_BASE, minutes % DIGIT_BASE, seconds / DIGIT_BASE, seconds % DIGIT_BASE);

            _fadeElapsed = 0f;
            _group.alpha = 0f;
            _fading = true;

            // Retry waits until the view has finished appearing, so a tap meant for the game is not taken as Retry,
            // and it is accepted once only.
            _shownAt = _clock();
            _retryReady = false;
            _retryConsumed = false;
            _retryButton.interactable = false;
        }

        /// <summary>
        /// Hides the view at once.
        /// </summary>
        public void Hide()
        {
            _fading = false;
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

        /// <summary>
        /// Raises <see cref="RetryClicked"/> for a click on the Retry button.
        /// </summary>
        private void HandleRetryClicked()
        {
            if (!_retryReady || _retryConsumed)
            {
                return;
            }

            _retryConsumed = true;
            _retryButton.interactable = false;
            RetryClicked?.Invoke();
        }

        /// <summary>
        /// Reads the real unscaled time, used until <see cref="Initialize"/> gives another clock.
        /// </summary>
        private static double UnscaledNow()
        {
            return Time.unscaledTimeAsDouble;
        }
    }
}
