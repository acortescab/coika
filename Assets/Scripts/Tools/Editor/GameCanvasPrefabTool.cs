using System;
using System.Collections.Generic;
using Coika.Core;
using Coika.UI;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using UnityEditor.Localization;

namespace Coika.Tools
{
    /// <summary>
    /// Builds <c>GameCanvas.prefab</c> (issue #10) and makes it Addressable in the UI group. The TMP default
    /// font is an SDF asset in Assets/Art/Fonts, Addressable in the same group (C-01). The canvas is Screen Space Overlay so the Pixel Perfect Camera cannot blur it, scales
    /// with the screen from a 1080 x 1920 reference, and carries its own EventSystem with the Input System UI
    /// module. The HUD sits under a <see cref="SafeAreaFitter"/>. Static texts are localized labels bound to the
    /// "UI" string table; the numbers are written by the views.
    /// <para>
    /// The prefab is rebuilt from scratch on every run, so its content always matches this file. Sizes are in
    /// reference pixels; a 180 x 320 reference pixel is 6 reference units of this canvas, so the 44 px touch target
    /// of the GDD is <see cref="MIN_TOUCH_SIZE"/>.
    /// </para>
    /// </summary>
    public static class GameCanvasPrefabTool
    {
        public const string PrefabPath = "Assets/Prefabs/UI/GameCanvas.prefab";
        public const string SettingsPrefabPath = "Assets/Prefabs/UI/SettingsView.prefab";
        public const string UiGroupName = "UI";

        /// <summary>Reference width of the canvas, in canvas units.</summary>
        public const float REFERENCE_WIDTH = 1080f;

        /// <summary>Reference height of the canvas, in canvas units.</summary>
        public const float REFERENCE_HEIGHT = 1920f;

        /// <summary>Minimum side of a touch target: 44 px of the 180 x 320 reference, scaled to the canvas.</summary>
        public const float MIN_TOUCH_SIZE = 44f * (REFERENCE_WIDTH / 180f);

        private const string FontFolder = "Assets/Art/Fonts";
        private const string FontAssetPath = FontFolder + "/UiFont SDF.asset";
        private const string SourceFontPath = FontFolder + "/LiberationSans.ttf";
        private const string LicensePath = FontFolder + "/LICENSE.txt";
        private const string TmpSourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        private const string TmpLicensePath = "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt";
        private const int SAMPLING_POINT_SIZE = 90;
        private const int ATLAS_PADDING = 5;
        private const int ATLAS_SIZE = 1024;
        private const float STAT_CAPTION_HEIGHT = 60f;
        private const float STAT_CAPTION_SIZE = 44f;
        private const float MARGIN = 40f;
        private const float MATCH_WIDTH_OR_HEIGHT = 0.5f;

        // Nine rows must fit one portrait panel, so the Settings controls are about 9 mm tall (GDD §8.5 ≈ 7 % to
        // 14 % of the reference width) instead of MIN_TOUCH_SIZE.
        private const float SETTINGS_ROW_HEIGHT = 140f;
        private const float SETTINGS_ROW_STEP = 150f;
        private const float SETTINGS_FIRST_ROW_Y = 590f;
        private const float SETTINGS_LABEL_X = -150f;
        private const float SETTINGS_LABEL_WIDTH = 520f;
        private const float SETTINGS_LABEL_SIZE = 46f;
        private const float SETTINGS_SLIDER_X = 290f;
        private const float SETTINGS_SLIDER_WIDTH = 340f;
        private const float SETTINGS_TOGGLE_X = 190f;
        private const float SETTINGS_TOGGLE_SIZE = 120f;
        private const float SETTINGS_STATE_X = 360f;
        private const float SETTINGS_STATE_WIDTH = 190f;

        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.14f, 0.92f);
        private static readonly Color FrameColor = new Color(0.12f, 0.12f, 0.2f, 0.85f);
        private static readonly Color ButtonColor = new Color(0.25f, 0.25f, 0.4f, 1f);
        private static readonly Color PrimaryButtonColor = new Color(0.2f, 0.55f, 0.35f, 1f);
        private static readonly Color AccentColor = new Color(1f, 0.85f, 0.25f, 1f);

        /// <summary>
        /// Builds the prefab and registers it and its font in the UI Addressables group. Aborts with an error,
        /// saving nothing, when the TMP font or the "UI" string table is missing.
        /// </summary>
        [MenuItem("Coika/Build Game Canvas Prefab")]
        public static void Run()
        {
            var font = EnsureFont();
            if (font == null)
            {
                return;
            }

            if (LocalizationEditorSettings.GetStringTableCollection(UiStrings.TableName) == null)
            {
                Debug.LogError($"String table '{UiStrings.TableName}' not found. Run Coika > Setup Localization first.");
                return;
            }

            var root = new GameObject("GameCanvas", typeof(RectTransform));
            try
            {
                Build(root, font);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            var settingsRoot = new GameObject("SettingsView", typeof(RectTransform));
            try
            {
                BuildSettings(settingsRoot.GetComponent<RectTransform>(), font);
                PrefabUtility.SaveAsPrefabAsset(settingsRoot, SettingsPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingsRoot);
            }

            MakeAddressable(PrefabPath);
            MakeAddressable(SettingsPrefabPath);
            MakeAddressable(FontAssetPath);
            Debug.Log($"Game canvas prefab is up to date at {PrefabPath}, and the Settings prefab at {SettingsPrefabPath}.");
        }

        /// <summary>
        /// Returns the SDF font asset of the UI, creating it when it is missing: a copy of the Liberation Sans font
        /// that ships with TMP (with its license) in <c>Assets/Art/Fonts</c>, as a dynamic SDF asset with padding
        /// 5. It lives outside <c>Resources/</c> so it can be Addressable (C-01); replacing it with a pixel font is
        /// a data change. Logs an error and returns null when TMP Essentials are not imported.
        /// </summary>
        private static TMP_FontAsset EnsureFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            if (AssetDatabase.LoadAssetAtPath<Font>(TmpSourceFontPath) == null)
            {
                Debug.LogError($"Source font not found at {TmpSourceFontPath}. Import it with Window > TextMeshPro > Import TMP Essential Resources.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(FontFolder))
            {
                AssetDatabase.CreateFolder("Assets/Art", "Fonts");
            }

            CopyIfMissing(TmpSourceFontPath, SourceFontPath);
            CopyIfMissing(TmpLicensePath, LicensePath);
            return CreateFontAsset();
        }

        /// <summary>
        /// Creates the dynamic SDF font asset from the copied source font and saves it with its atlas texture and
        /// material as sub-assets.
        /// </summary>
        private static TMP_FontAsset CreateFontAsset()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            var asset = TMP_FontAsset.CreateFontAsset(source, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.SDFAA, ATLAS_SIZE, ATLAS_SIZE, AtlasPopulationMode.Dynamic);
            asset.name = "UiFont SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);

            asset.atlasTexture.name = "UiFont Atlas";
            asset.material.name = "UiFont Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// Copies an asset inside the project when the destination does not exist yet.
        /// </summary>
        private static void CopyIfMissing(string from, string to)
        {
            if (AssetDatabase.LoadMainAssetAtPath(to) == null && AssetDatabase.LoadMainAssetAtPath(from) != null)
            {
                AssetDatabase.CopyAsset(from, to);
            }
        }

        /// <summary>
        /// Builds the whole hierarchy under the root: canvas components, EventSystem, safe area, HUD and Game Over.
        /// </summary>
        private static void Build(GameObject root, TMP_FontAsset font)
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MATCH_WIDTH_OR_HEIGHT;

            root.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(root.transform, false);
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var safeArea = NewRect("SafeArea", root.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            BuildHud(safeArea, font);
            BuildGameOver(safeArea, font);
            BuildPause(safeArea, font);
            BuildConfirm(safeArea, font);
        }

        /// <summary>
        /// Builds the pause menu, inactive: a dimmed backdrop and the Resume, Restart, Settings and Menu buttons.
        /// Menu is disabled by the view until M3.
        /// </summary>
        private static void BuildPause(RectTransform parent, TMP_FontAsset font)
        {
            var pauseRect = NewRect("Pause", parent);
            Stretch(pauseRect);
            pauseRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var view = pauseRect.gameObject.AddComponent<PauseView>();

            var panel = NewRect("Panel", pauseRect);
            Place(panel, Centered(0f, 0f, 960f, 1700f));
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            AddText(panel, "Title", font, UiStrings.PauseTitle, Centered(0f, 700f, 880f, 120f), 96f, TextAlignmentOptions.Center, Color.white);

            const float buttonWidth = 640f;
            var resume = AddButton(panel, "ResumeButton", font, UiStrings.PauseResume, Centered(0f, 330f, buttonWidth, MIN_TOUCH_SIZE), PrimaryButtonColor, 72f);
            var restart = AddButton(panel, "RestartButton", font, UiStrings.PauseRestart, Centered(0f, 30f, buttonWidth, MIN_TOUCH_SIZE), ButtonColor, 72f);
            var settings = AddButton(panel, "SettingsButton", font, UiStrings.PauseSettings, Centered(0f, -270f, buttonWidth, MIN_TOUCH_SIZE), ButtonColor, 72f);
            var menu = AddButton(panel, "MenuButton", font, UiStrings.PauseMenu, Centered(0f, -570f, buttonWidth, MIN_TOUCH_SIZE), ButtonColor, 72f);

            Assign(view, "_resumeButton", resume);
            Assign(view, "_restartButton", restart);
            Assign(view, "_settingsButton", settings);
            Assign(view, "_menuButton", menu);
            pauseRect.gameObject.SetActive(false);
        }

        /// <summary>
        /// Builds the reusable confirmation dialog, inactive, over the pause menu. Its title and message are set
        /// at runtime from a request; the Restart texts are only the defaults shown in the Editor.
        /// </summary>
        private static void BuildConfirm(RectTransform parent, TMP_FontAsset font)
        {
            var confirmRect = NewRect("Confirm", parent);
            Stretch(confirmRect);
            confirmRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var view = confirmRect.gameObject.AddComponent<ConfirmView>();

            var panel = NewRect("Panel", confirmRect);
            Place(panel, Centered(0f, 0f, 960f, 900f));
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            var title = AddText(panel, "Title", font, UiTextKeys.CONFIRM_RESTART_TITLE, Centered(0f, 300f, 880f, 120f), 80f, TextAlignmentOptions.Center, Color.white);
            var message = AddText(panel, "Message", font, UiTextKeys.CONFIRM_RESTART_MESSAGE, Centered(0f, 60f, 840f, 300f), 56f, TextAlignmentOptions.Center, Color.gray);
            message.textWrappingMode = TextWrappingModes.Normal;

            var confirm = AddButton(panel, "ConfirmButton", font, UiStrings.ConfirmYes, Centered(230f, -280f, 400f, MIN_TOUCH_SIZE), PrimaryButtonColor, 64f);
            var cancel = AddButton(panel, "CancelButton", font, UiStrings.ConfirmCancel, Centered(-230f, -280f, 400f, MIN_TOUCH_SIZE), ButtonColor, 64f);

            Assign(view, "_title", title.GetComponent<LocalizeStringEvent>());
            Assign(view, "_message", message.GetComponent<LocalizeStringEvent>());
            Assign(view, "_confirmButton", confirm);
            Assign(view, "_cancelButton", cancel);
            confirmRect.gameObject.SetActive(false);
        }

        /// <summary>
        /// Builds the Settings screen as its own prefab, inactive, so the Pause menu and the Menu (M3) can push the
        /// same one: a dimmed backdrop over the safe area, and a panel with a row per setting (a slider or a toggle
        /// with an ON or OFF text), the disabled Language row, Reset progress and Back. The rows are the settings of
        /// <see cref="SettingsRows"/>, in that order.
        /// </summary>
        /// <param name="root">The root of the prefab, which becomes the view.</param>
        /// <param name="font">The font of every text.</param>
        private static void BuildSettings(RectTransform root, TMP_FontAsset font)
        {
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var view = root.gameObject.AddComponent<SettingsView>();

            var panel = NewRect("Panel", root);
            Place(panel, Centered(0f, 0f, 960f, 1700f));
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            AddText(panel, "Title", font, UiStrings.SettingsTitle, Centered(0f, 760f, 880f, 120f), 90f, TextAlignmentOptions.Center, Color.white);

            var row = 0;
            var sliders = new List<Slider>();
            foreach (var key in SettingsRows.Sliders)
            {
                var y = AddRowLabel(panel, font, key.ToString(), UiStrings.SettingLabels[key], row++);
                sliders.Add(AddSlider(panel, key + "Slider", y));
            }

            var toggles = new List<Toggle>();
            var states = new List<LocalizeStringEvent>();
            foreach (var key in SettingsRows.Toggles)
            {
                var y = AddRowLabel(panel, font, key.ToString(), UiStrings.SettingLabels[key], row++);
                toggles.Add(AddToggle(panel, key + "Toggle", y));
                var state = AddText(panel, key + "State", font, UiTextKeys.SETTINGS_OFF, Centered(SETTINGS_STATE_X, y, SETTINGS_STATE_WIDTH, SETTINGS_ROW_HEIGHT), SETTINGS_LABEL_SIZE, TextAlignmentOptions.Center, Color.white);
                states.Add(state.GetComponent<LocalizeStringEvent>());
            }

            // The language is fixed to English until the localization of M3, so its row is a disabled placeholder.
            var languageY = AddRowLabel(panel, font, "Language", UiStrings.SettingsLanguage, row++);
            var language = AddButton(panel, "LanguageButton", font, UiStrings.SettingsLanguageValue, Centered(SETTINGS_SLIDER_X, languageY, SETTINGS_SLIDER_WIDTH, SETTINGS_ROW_HEIGHT), ButtonColor, SETTINGS_LABEL_SIZE);
            language.interactable = false;

            const float buttonWidth = 640f;
            var reset = AddButton(panel, "ResetButton", font, UiStrings.SettingsReset, Centered(0f, SettingsRowY(row++), buttonWidth, SETTINGS_ROW_HEIGHT), new Color(0.6f, 0.2f, 0.2f, 1f), 56f);
            var back = AddButton(panel, "BackButton", font, UiStrings.SettingsBack, Centered(0f, SettingsRowY(row), buttonWidth, SETTINGS_ROW_HEIGHT), PrimaryButtonColor, 56f);

            AssignRows(view, "_sliders", SettingsRows.Sliders, ("_slider", sliders.ToArray()));
            AssignRows(view, "_toggles", SettingsRows.Toggles, ("_toggle", toggles.ToArray()), ("_stateLabel", states.ToArray()));
            Assign(view, "_resetButton", reset);
            Assign(view, "_backButton", back);
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The vertical position of a row of the Settings panel, counted from the top.
        /// </summary>
        /// <param name="row">Index of the row, 0 for the first.</param>
        private static float SettingsRowY(int row)
        {
            return SETTINGS_FIRST_ROW_Y - SETTINGS_ROW_STEP * row;
        }

        /// <summary>
        /// Adds the localized, left-aligned name of a setting at the left of a row.
        /// </summary>
        /// <param name="parent">The panel.</param>
        /// <param name="font">The font of the label.</param>
        /// <param name="name">Name of the row, used for the objects of the row.</param>
        /// <param name="key">String key of the text.</param>
        /// <param name="row">Index of the row, 0 for the first.</param>
        /// <returns>The vertical position of the row, for the control that goes beside the label.</returns>
        private static float AddRowLabel(RectTransform parent, TMP_FontAsset font, string name, string key, int row)
        {
            var y = SettingsRowY(row);
            var label = AddText(parent, name + "Label", font, key, Centered(SETTINGS_LABEL_X, y, SETTINGS_LABEL_WIDTH, SETTINGS_ROW_HEIGHT), SETTINGS_LABEL_SIZE, TextAlignmentOptions.Left, Color.white);
            label.textWrappingMode = TextWrappingModes.Normal;
            return y;
        }

        /// <summary>
        /// Adds a 0 to 1 slider: a thin bar, a fill and a handle, inside a transparent row-high rect that takes the
        /// touches, so the bar is easy to hit.
        /// </summary>
        /// <param name="parent">The panel.</param>
        /// <param name="name">Name of the slider object.</param>
        /// <param name="y">Vertical position of the row.</param>
        /// <returns>The slider.</returns>
        private static Slider AddSlider(RectTransform parent, string name, float y)
        {
            var rect = NewRect(name, parent);
            Place(rect, Centered(SETTINGS_SLIDER_X, y, SETTINGS_SLIDER_WIDTH, SETTINGS_ROW_HEIGHT));
            rect.gameObject.AddComponent<Image>().color = Color.clear;

            var bar = NewRect("Background", rect);
            bar.anchorMin = new Vector2(0f, 0.5f);
            bar.anchorMax = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(-SETTINGS_TOGGLE_SIZE * 0.5f, 30f);
            bar.gameObject.AddComponent<Image>().color = ButtonColor;

            var fillArea = NewRect("Fill Area", rect);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-SETTINGS_TOGGLE_SIZE * 0.5f, 30f);
            var fill = NewRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.sizeDelta = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = PrimaryButtonColor;

            var handleArea = NewRect("Handle Slide Area", rect);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(SETTINGS_TOGGLE_SIZE * 0.25f, 0f);
            handleArea.offsetMax = new Vector2(-SETTINGS_TOGGLE_SIZE * 0.25f, 0f);
            var handle = NewRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0.5f);
            handle.anchorMax = new Vector2(0f, 0.5f);
            handle.sizeDelta = new Vector2(SETTINGS_TOGGLE_SIZE * 0.5f, SETTINGS_TOGGLE_SIZE * 0.8f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = AccentColor;

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        /// <summary>
        /// Adds a square toggle with a checkmark. The ON or OFF text beside it is added by the caller, because the
        /// state is never only a colour.
        /// </summary>
        /// <param name="parent">The panel.</param>
        /// <param name="name">Name of the toggle object.</param>
        /// <param name="y">Vertical position of the row.</param>
        /// <returns>The toggle.</returns>
        private static Toggle AddToggle(RectTransform parent, string name, float y)
        {
            var rect = NewRect(name, parent);
            Place(rect, Centered(SETTINGS_TOGGLE_X, y, SETTINGS_TOGGLE_SIZE, SETTINGS_TOGGLE_SIZE));
            var background = rect.gameObject.AddComponent<Image>();
            background.color = ButtonColor;

            var check = NewRect("Checkmark", rect);
            Stretch(check);
            check.offsetMin = new Vector2(20f, 20f);
            check.offsetMax = new Vector2(-20f, -20f);
            var checkImage = check.gameObject.AddComponent<Image>();
            checkImage.color = AccentColor;
            checkImage.raycastTarget = false;

            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkImage;
            toggle.isOn = false;
            return toggle;
        }

        /// <summary>
        /// Fills an array of rows of the view: the setting of each row, and one object per column (the slider, the
        /// switch, the state text), in the order of the keys.
        /// </summary>
        /// <param name="view">The Settings view.</param>
        /// <param name="field">The serialized array of rows.</param>
        /// <param name="keys">The setting of each row.</param>
        /// <param name="columns">The field of the row and its object for each row.</param>
        private static void AssignRows(SettingsView view, string field, SettingKey[] keys, params (string Field, UnityEngine.Object[] Values)[] columns)
        {
            var serialized = new SerializedObject(view);
            var rows = serialized.FindProperty(field);
            rows.arraySize = keys.Length;
            for (var i = 0; i < keys.Length; i++)
            {
                var element = rows.GetArrayElementAtIndex(i);
                SetKey(element.FindPropertyRelative("_key"), keys[i]);
                foreach (var column in columns)
                {
                    element.FindPropertyRelative(column.Field).objectReferenceValue = column.Values[i];
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        /// <summary>
        /// Sets an enum property by the name of the value, so it does not depend on the order of the enum.
        /// </summary>
        /// <param name="property">The serialized enum.</param>
        /// <param name="key">The value to set.</param>
        private static void SetKey(SerializedProperty property, SettingKey key)
        {
            property.enumValueIndex = Array.IndexOf(property.enumNames, key.ToString());
        }

        /// <summary>
        /// Builds the HUD: score and best score top-left, combo under them, pause button and next-piece box
        /// top-right.
        /// </summary>
        private static void BuildHud(RectTransform parent, TMP_FontAsset font)
        {
            var hudRect = NewRect("Hud", parent);
            Stretch(hudRect);
            var hud = hudRect.gameObject.AddComponent<HudView>();

            var score = AddText(hudRect, "ScoreText", font, null, TopLeft(MARGIN, -MARGIN, 420f, 120f), 90f, TextAlignmentOptions.TopLeft, Color.white);
            score.enableAutoSizing = true;
            score.fontSizeMin = 40f;
            score.fontSizeMax = 90f;

            AddText(hudRect, "BestCaption", font, UiStrings.HudBest, TopLeft(MARGIN, -170f, 160f, 60f), 40f, TextAlignmentOptions.TopLeft, Color.gray);
            var best = AddText(hudRect, "BestText", font, null, TopLeft(MARGIN + 170f, -170f, 270f, 60f), 40f, TextAlignmentOptions.TopLeft, Color.gray);
            var combo = AddText(hudRect, "ComboText", font, null, TopLeft(MARGIN, -240f, 440f, 80f), 64f, TextAlignmentOptions.TopLeft, AccentColor);

            var pause = AddButton(hudRect, "PauseButton", font, null, TopRight(-(MARGIN + MIN_TOUCH_SIZE + 32f), -MARGIN, MIN_TOUCH_SIZE, MIN_TOUCH_SIZE), ButtonColor, 96f);
            // "II" is a symbol, not a word, so it is plain text and has no localization key (S-94).
            pause.GetComponentInChildren<TMP_Text>().text = "II";

            var frame = NewRect("NextFrame", hudRect);
            Place(frame, TopRight(-MARGIN, -MARGIN, MIN_TOUCH_SIZE, MIN_TOUCH_SIZE));
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = FrameColor;
            frameImage.raycastTarget = false;

            var preview = NewRect("NextPreview", frame);
            Stretch(preview);
            preview.offsetMin = new Vector2(24f, 24f);
            preview.offsetMax = new Vector2(-24f, -24f);
            var previewImage = preview.gameObject.AddComponent<Image>();
            previewImage.preserveAspect = true;
            previewImage.raycastTarget = false;

            Assign(hud, "_scoreText", score);
            Assign(hud, "_bestText", best);
            Assign(hud, "_comboText", combo);
            Assign(hud, "_nextPreview", previewImage);
            Assign(hud, "_pauseButton", pause);
            combo.gameObject.SetActive(false);
        }

        /// <summary>
        /// Builds the Game Over panel, inactive: a dimmed backdrop, the stats, and the Retry and Menu buttons.
        /// </summary>
        private static void BuildGameOver(RectTransform parent, TMP_FontAsset font)
        {
            var overRect = NewRect("GameOver", parent);
            Stretch(overRect);
            overRect.gameObject.AddComponent<CanvasGroup>();
            var view = overRect.gameObject.AddComponent<GameOverView>();

            var backdrop = overRect.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.6f);

            var panel = NewRect("Panel", overRect);
            Place(panel, Centered(0f, 0f, 960f, 1500f));
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            const float fullWidth = 880f;
            AddText(panel, "Title", font, UiStrings.GameOverTitle, Centered(0f, 660f, fullWidth, 120f), 96f, TextAlignmentOptions.Center, Color.white);

            var banner = NewRect("NewBestBanner", panel);
            Place(banner, Centered(0f, 530f, fullWidth, 90f));
            AddText(banner, "NewBestText", font, UiStrings.GameOverNewBest, Centered(0f, 0f, fullWidth, 90f), 72f, TextAlignmentOptions.Center, AccentColor);

            var score = AddStat(panel, font, "Score", UiStrings.GameOverScore, 0f, 410f, 320f, fullWidth, 130f, 120f);
            var best = AddStat(panel, font, "Best", UiStrings.GameOverBest, 0f, 210f, 130f, fullWidth, 100f, 80f);

            AddText(panel, "HighestTierCaption", font, UiStrings.GameOverHighestTier, Centered(0f, 30f, fullWidth, STAT_CAPTION_HEIGHT), STAT_CAPTION_SIZE, TextAlignmentOptions.Center, Color.gray);
            var iconRect = NewRect("HighestTierIcon", panel);
            Place(iconRect, Centered(0f, -90f, 160f, 160f));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var pieces = AddStat(panel, font, "Pieces", UiStrings.GameOverPieces, -220f, -250f, -320f, 400f, 90f, 72f);
            var time = AddStat(panel, font, "Time", UiStrings.GameOverTime, 220f, -250f, -320f, 400f, 90f, 72f);

            var retry = AddButton(panel, "RetryButton", font, UiStrings.GameOverRetry, Centered(-230f, -520f, 440f, MIN_TOUCH_SIZE), PrimaryButtonColor, 64f);
            var menu = AddButton(panel, "MenuButton", font, UiStrings.GameOverMenu, Centered(230f, -520f, 440f, MIN_TOUCH_SIZE), ButtonColor, 64f);

            Assign(view, "_scoreText", score);
            Assign(view, "_bestText", best);
            Assign(view, "_newBestBanner", banner.gameObject);
            Assign(view, "_highestTierIcon", icon);
            Assign(view, "_piecesText", pieces);
            Assign(view, "_timeText", time);
            Assign(view, "_retryButton", retry);
            Assign(view, "_menuButton", menu);
            overRect.gameObject.SetActive(false);
        }

        /// <summary>
        /// Anchoring values of a rect, so the layout above reads as a list of positions.
        /// </summary>
        private readonly struct Placement
        {
            public Placement(Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
            {
                Anchor = anchor;
                Pivot = pivot;
                Position = position;
                Size = size;
            }

            public Vector2 Anchor { get; }
            public Vector2 Pivot { get; }
            public Vector2 Position { get; }
            public Vector2 Size { get; }
        }

        private static Placement TopLeft(float x, float y, float width, float height)
        {
            return new Placement(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
        }

        private static Placement TopRight(float x, float y, float width, float height)
        {
            return new Placement(Vector2.one, Vector2.one, new Vector2(x, y), new Vector2(width, height));
        }

        private static Placement Centered(float x, float y, float width, float height)
        {
            return new Placement(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height));
        }

        /// <summary>
        /// Creates an empty child with a RectTransform.
        /// </summary>
        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>
        /// Makes a rect fill its parent.
        /// </summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Applies a placement to a rect.
        /// </summary>
        private static void Place(RectTransform rect, Placement placement)
        {
            rect.anchorMin = placement.Anchor;
            rect.anchorMax = placement.Anchor;
            rect.pivot = placement.Pivot;
            rect.anchoredPosition = placement.Position;
            rect.sizeDelta = placement.Size;
        }

        /// <summary>
        /// Adds a TMP label. A non-null key binds it to the string table with a localize-string event; a null key
        /// leaves it for a view to fill with a number. Labels never take raycasts.
        /// </summary>
        private static TMP_Text AddText(RectTransform parent, string name, TMP_FontAsset font, string key, Placement placement, float size, TextAlignmentOptions alignment, Color color)
        {
            var rect = NewRect(name, parent);
            Place(rect, placement);

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            if (key != null)
            {
                Localize(text, key);
            }

            return text;
        }

        /// <summary>
        /// Adds a button with a background image and a label, which is localized when it has a key. The label is
        /// found again with <c>GetComponentInChildren</c> by a caller that wants to set a symbol instead.
        /// </summary>
        private static Button AddButton(RectTransform parent, string name, TMP_FontAsset font, string key, Placement placement, Color color, float fontSize)
        {
            var rect = NewRect(name, parent);
            Place(rect, placement);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            AddText(rect, "Label", font, key, Centered(0f, 0f, placement.Size.x, placement.Size.y), fontSize, TextAlignmentOptions.Center, Color.white);
            return button;
        }

        /// <summary>
        /// Adds a gray localized caption with a value label under it, for the Game Over stats, and returns the
        /// value label. The value is empty here: the view writes it.
        /// </summary>
        private static TMP_Text AddStat(RectTransform parent, TMP_FontAsset font, string name, string key, float x, float captionY, float valueY, float width, float valueHeight, float valueSize)
        {
            AddText(parent, name + "Caption", font, key, Centered(x, captionY, width, STAT_CAPTION_HEIGHT), STAT_CAPTION_SIZE, TextAlignmentOptions.Center, Color.gray);
            return AddText(parent, name + "Text", font, null, Centered(x, valueY, width, valueHeight), valueSize, TextAlignmentOptions.Center, Color.white);
        }

        /// <summary>
        /// Binds a label to a key of the UI string table: a localize-string event whose update calls the text setter
        /// of the label, stored as a persistent listener so it works without code at runtime.
        /// </summary>
        private static void Localize(TMP_Text text, string key)
        {
            var localize = text.gameObject.AddComponent<LocalizeStringEvent>();
            localize.StringReference = new LocalizedString(UiStrings.TableName, key);

            var setter = typeof(TMP_Text).GetProperty(nameof(TMP_Text.text)).GetSetMethod();
            var call = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, setter);
            UnityEventTools.AddPersistentListener(localize.OnUpdateString, call);
        }

        /// <summary>
        /// Sets a private serialized field of a component, the way the Inspector would.
        /// </summary>
        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Registers an asset in the UI Addressables group. Logs an error when the group is missing.
        /// </summary>
        private static void MakeAddressable(string path)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings != null ? settings.FindGroup(UiGroupName) : null;
            if (group == null)
            {
                Debug.LogError($"Addressables group '{UiGroupName}' not found; {path} was not made Addressable.");
                return;
            }

            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
            AssetDatabase.SaveAssets();
        }
    }
}
