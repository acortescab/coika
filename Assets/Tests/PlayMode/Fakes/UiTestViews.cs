using System.Reflection;
using Coika.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Builds the HUD and Game Over views in code for the PlayMode tests, without the Addressable canvas prefab
    /// (S-121). The views start on inactive objects, so their serialized fields are set before Awake runs, the
    /// same order a prefab gives.
    /// </summary>
    public sealed class UiTestViews
    {
        private const BindingFlags PRIVATE_FIELD = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly GameObject _root = new GameObject("UiTestViews");

        /// <summary>
        /// Builds both views. The Game Over view is left inactive, as in the prefab.
        /// </summary>
        public UiTestViews()
        {
            _root.SetActive(false);
            // TextMeshProUGUI builds its text only under a canvas, so GetParsedText would be empty without one.
            _root.AddComponent<Canvas>();
            BuildHud();
            BuildGameOver();
            BuildPause();
            BuildConfirm();
            _root.SetActive(true);
            GameOverObject.SetActive(false);
            PauseObject.SetActive(false);
            ConfirmObject.SetActive(false);
        }

        public PauseView Pause { get; private set; }
        public GameObject PauseObject { get; private set; }
        public Button PauseResume { get; private set; }
        public Button PauseRestart { get; private set; }
        public Button PauseSettings { get; private set; }
        public Button PauseMenu { get; private set; }

        public ConfirmView Confirm { get; private set; }
        public GameObject ConfirmObject { get; private set; }
        public Button ConfirmYes { get; private set; }
        public Button ConfirmCancel { get; private set; }

        public HudView Hud { get; private set; }
        public TMP_Text HudScore { get; private set; }
        public TMP_Text HudBest { get; private set; }
        public TMP_Text HudCombo { get; private set; }
        public Image HudNext { get; private set; }
        public Button HudPause { get; private set; }

        public GameOverView GameOver { get; private set; }
        public GameObject GameOverObject { get; private set; }
        public TMP_Text OverScore { get; private set; }
        public TMP_Text OverBest { get; private set; }
        public GameObject OverNewBest { get; private set; }
        public Image OverIcon { get; private set; }
        public TMP_Text OverPieces { get; private set; }
        public TMP_Text OverTime { get; private set; }
        public Button OverRetry { get; private set; }
        public Button OverMenu { get; private set; }
        public CanvasGroup OverGroup { get; private set; }

        /// <summary>
        /// The text a label shows after its formatting was applied; <c>SetText</c> does not update <c>text</c>.
        /// </summary>
        /// <param name="label">The label.</param>
        public static string Shown(TMP_Text label)
        {
            label.ForceMeshUpdate();
            return label.GetParsedText();
        }

        /// <summary>
        /// Destroys every object the builder created.
        /// </summary>
        public void Destroy()
        {
            Object.Destroy(_root);
        }

        private void BuildHud()
        {
            var hudObject = Child("Hud", _root.transform);
            Hud = hudObject.AddComponent<HudView>();
            HudScore = Label("Score", hudObject.transform);
            HudBest = Label("Best", hudObject.transform);
            HudCombo = Label("Combo", hudObject.transform);
            HudNext = Child("Next", hudObject.transform).AddComponent<Image>();
            HudPause = Child("Pause", hudObject.transform).AddComponent<Button>();

            Set(Hud, "_scoreText", HudScore);
            Set(Hud, "_bestText", HudBest);
            Set(Hud, "_comboText", HudCombo);
            Set(Hud, "_nextPreview", HudNext);
            Set(Hud, "_pauseButton", HudPause);
        }

        private void BuildGameOver()
        {
            GameOverObject = Child("GameOver", _root.transform);
            OverGroup = GameOverObject.AddComponent<CanvasGroup>();
            GameOver = GameOverObject.AddComponent<GameOverView>();
            OverScore = Label("Score", GameOverObject.transform);
            OverBest = Label("Best", GameOverObject.transform);
            OverNewBest = Child("NewBest", GameOverObject.transform);
            OverIcon = Child("Icon", GameOverObject.transform).AddComponent<Image>();
            OverPieces = Label("Pieces", GameOverObject.transform);
            OverTime = Label("Time", GameOverObject.transform);
            OverRetry = Child("Retry", GameOverObject.transform).AddComponent<Button>();
            OverMenu = Child("Menu", GameOverObject.transform).AddComponent<Button>();

            Set(GameOver, "_scoreText", OverScore);
            Set(GameOver, "_bestText", OverBest);
            Set(GameOver, "_newBestBanner", OverNewBest);
            Set(GameOver, "_highestTierIcon", OverIcon);
            Set(GameOver, "_piecesText", OverPieces);
            Set(GameOver, "_timeText", OverTime);
            Set(GameOver, "_retryButton", OverRetry);
            Set(GameOver, "_menuButton", OverMenu);
        }

        private void BuildPause()
        {
            PauseObject = Child("Pause", _root.transform);
            Pause = PauseObject.AddComponent<PauseView>();
            PauseResume = Child("Resume", PauseObject.transform).AddComponent<Button>();
            PauseRestart = Child("Restart", PauseObject.transform).AddComponent<Button>();
            PauseSettings = Child("Settings", PauseObject.transform).AddComponent<Button>();
            PauseMenu = Child("Menu", PauseObject.transform).AddComponent<Button>();

            Set(Pause, "_resumeButton", PauseResume);
            Set(Pause, "_restartButton", PauseRestart);
            Set(Pause, "_settingsButton", PauseSettings);
            Set(Pause, "_menuButton", PauseMenu);
        }

        private void BuildConfirm()
        {
            ConfirmObject = Child("Confirm", _root.transform);
            Confirm = ConfirmObject.AddComponent<ConfirmView>();
            ConfirmYes = Child("Yes", ConfirmObject.transform).AddComponent<Button>();
            ConfirmCancel = Child("Cancel", ConfirmObject.transform).AddComponent<Button>();

            Set(Confirm, "_title", Child("Title", ConfirmObject.transform).AddComponent<LocalizeStringEvent>());
            Set(Confirm, "_message", Child("Message", ConfirmObject.transform).AddComponent<LocalizeStringEvent>());
            Set(Confirm, "_confirmButton", ConfirmYes);
            Set(Confirm, "_cancelButton", ConfirmCancel);
        }

        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static TMP_Text Label(string name, Transform parent)
        {
            return Child(name, parent).AddComponent<TextMeshProUGUI>();
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, PRIVATE_FIELD).SetValue(target, value);
        }
    }
}
