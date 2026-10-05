using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// Passive view of the in-game HUD (GDD §8.1): score, best score, combo multiplier, the next-piece preview and
    /// the pause button. It holds no rules and reads no system (S-91); the <see cref="HudPresenter"/> pushes values
    /// into it. Numbers go through <c>TMP_Text.SetText</c> with a format, so showing them allocates nothing (S-53).
    /// <para>
    /// The captions ("Best") are separate labels with localize-string events; this view only writes the numbers.
    /// The pause button only raises <see cref="PauseClicked"/>; the installer pauses the game.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/UI/Hud View")]
    [DisallowMultipleComponent]
    public class HudView : MonoBehaviour
    {
        private const string SCORE_FORMAT = "{0}";
        private const string COMBO_FORMAT = "x{0:2}";

        [SerializeField]
        private TMP_Text _scoreText;
        [SerializeField]
        private TMP_Text _bestText;
        [SerializeField]
        private TMP_Text _comboText;
        [SerializeField]
        private Image _nextPreview;
        [SerializeField]
        private Button _pauseButton;

        private UnityAction _onPauseClicked;

        /// <summary>Raised once per click on the pause button.</summary>
        public event Action PauseClicked;

        /// <summary>
        /// Checks the serialized references and caches the click handler.
        /// </summary>
        private void Awake()
        {
            if (_scoreText == null || _bestText == null || _comboText == null || _nextPreview == null || _pauseButton == null)
            {
                Debug.LogError("HudView needs the score, best, combo, next preview and pause button references.", this);
            }

            _onPauseClicked = HandlePauseClicked;
        }

        /// <summary>
        /// Starts listening to the pause button while the view is on screen (S-23).
        /// </summary>
        private void OnEnable()
        {
            if (_pauseButton != null)
            {
                _pauseButton.onClick.AddListener(_onPauseClicked);
            }
        }

        /// <summary>
        /// Stops listening when the view is hidden or destroyed (S-23).
        /// </summary>
        private void OnDisable()
        {
            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(_onPauseClicked);
            }
        }

        /// <summary>
        /// Raises <see cref="PauseClicked"/>.
        /// </summary>
        private void HandlePauseClicked()
        {
            PauseClicked?.Invoke();
        }

        /// <summary>
        /// Shows the score. The number is passed as a float to <c>SetText</c>, which is exact up to 16,777,216.
        /// </summary>
        /// <param name="score">Score of the run.</param>
        public void SetScore(int score)
        {
            _scoreText.SetText(SCORE_FORMAT, score);
        }

        /// <summary>
        /// Shows the best score to beat.
        /// </summary>
        /// <param name="bestScore">Best score.</param>
        public void SetBest(int bestScore)
        {
            _bestText.SetText(SCORE_FORMAT, bestScore);
        }

        /// <summary>
        /// Shows the combo multiplier (x1.25, x1.5, ...), or hides the label when there is no bonus.
        /// </summary>
        /// <param name="multiplier">The combo multiplier; 1 or less hides the label.</param>
        public void SetCombo(float multiplier)
        {
            var visible = multiplier > 1f;
            if (_comboText.gameObject.activeSelf != visible)
            {
                _comboText.gameObject.SetActive(visible);
            }

            if (visible)
            {
                _comboText.SetText(COMBO_FORMAT, multiplier);
            }
        }

        /// <summary>
        /// Shows the sprite of the next piece in the preview box, fitted to the box with its aspect kept.
        /// </summary>
        /// <param name="sprite">Sprite of the tier that will be dropped next.</param>
        public void SetNextPreview(Sprite sprite)
        {
            _nextPreview.sprite = sprite;
            _nextPreview.enabled = sprite != null;
        }
    }
}
