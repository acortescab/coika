using System;
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
        private const float MARGIN = 40f;
        private const float MATCH_WIDTH_OR_HEIGHT = 0.5f;

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

            MakeAddressable(PrefabPath);
            MakeAddressable(FontAssetPath);
            Debug.Log($"Game canvas prefab is up to date at {PrefabPath}.");
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

            var pause = AddButton(hudRect, "PauseButton", font, null, "II", TopRight(-(MARGIN + MIN_TOUCH_SIZE + 32f), -MARGIN, MIN_TOUCH_SIZE, MIN_TOUCH_SIZE), ButtonColor, 96f);

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

            const float centerX = 0f;
            AddText(panel, "Title", font, UiStrings.GameOverTitle, Centered(centerX, 660f, 880f, 120f), 96f, TextAlignmentOptions.Center, Color.white);

            var banner = NewRect("NewBestBanner", panel);
            Place(banner, Centered(centerX, 530f, 880f, 90f));
            AddText(banner, "NewBestText", font, UiStrings.GameOverNewBest, Centered(0f, 0f, 880f, 90f), 72f, TextAlignmentOptions.Center, AccentColor);

            AddText(panel, "ScoreCaption", font, UiStrings.GameOverScore, Centered(centerX, 410f, 880f, 60f), 44f, TextAlignmentOptions.Center, Color.gray);
            var score = AddText(panel, "ScoreText", font, null, Centered(centerX, 320f, 880f, 130f), 120f, TextAlignmentOptions.Center, Color.white);

            AddText(panel, "BestCaption", font, UiStrings.GameOverBest, Centered(centerX, 210f, 880f, 60f), 44f, TextAlignmentOptions.Center, Color.gray);
            var best = AddText(panel, "BestText", font, null, Centered(centerX, 130f, 880f, 100f), 80f, TextAlignmentOptions.Center, Color.white);

            AddText(panel, "HighestTierCaption", font, UiStrings.GameOverHighestTier, Centered(centerX, 30f, 880f, 60f), 44f, TextAlignmentOptions.Center, Color.gray);
            var iconRect = NewRect("HighestTierIcon", panel);
            Place(iconRect, Centered(centerX, -90f, 160f, 160f));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            AddText(panel, "PiecesCaption", font, UiStrings.GameOverPieces, Centered(-220f, -250f, 400f, 60f), 44f, TextAlignmentOptions.Center, Color.gray);
            var pieces = AddText(panel, "PiecesText", font, null, Centered(-220f, -320f, 400f, 90f), 72f, TextAlignmentOptions.Center, Color.white);
            AddText(panel, "TimeCaption", font, UiStrings.GameOverTime, Centered(220f, -250f, 400f, 60f), 44f, TextAlignmentOptions.Center, Color.gray);
            var time = AddText(panel, "TimeText", font, null, Centered(220f, -320f, 400f, 90f), 72f, TextAlignmentOptions.Center, Color.white);

            var retry = AddButton(panel, "RetryButton", font, UiStrings.GameOverRetry, null, Centered(-230f, -520f, 440f, MIN_TOUCH_SIZE), PrimaryButtonColor, 64f);
            var menu = AddButton(panel, "MenuButton", font, UiStrings.GameOverMenu, null, Centered(230f, -520f, 440f, MIN_TOUCH_SIZE), ButtonColor, 64f);

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
        /// Adds a button with a background image and a label. The label is localized when it has a key, otherwise it
        /// shows the given glyph text, which is a symbol and not a word.
        /// </summary>
        private static Button AddButton(RectTransform parent, string name, TMP_FontAsset font, string key, string symbol, Placement placement, Color color, float fontSize)
        {
            var rect = NewRect(name, parent);
            Place(rect, placement);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = AddText(rect, "Label", font, key, Centered(0f, 0f, placement.Size.x, placement.Size.y), fontSize, TextAlignmentOptions.Center, Color.white);
            if (symbol != null)
            {
                label.text = symbol;
            }

            return button;
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
