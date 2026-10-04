using System.IO;
using Coika.Data;
using Coika.Gameplay;
using Coika.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Creates or updates the guide line assets of issue #28 and makes them Addressable in the Fx group (C-01): the
    /// tiled dot sprite and the GuideLine prefab (a <see cref="GuideLineView"/> with a line and a ghost renderer),
    /// then points the <see cref="GameSceneInstaller"/> of the Game scene at the prefab. Idempotent.
    /// </summary>
    public static class GuideLinePrefabTool
    {
        public const string PrefabPath = "Assets/Prefabs/Fx/GuideLine.prefab";
        public const string DotSpritePath = "Assets/Art/Sprites/Fx/GuideDot.png";
        public const string FxGroupName = "FX";

        /// <summary>Sorting order of the ghost: behind the pieces (2), in front of the jar walls (1).</summary>
        public const int GHOST_SORTING_ORDER = 1;

        /// <summary>Sorting order of the dotted line: above the pieces, below the Danger Line (10).</summary>
        public const int LINE_SORTING_ORDER = 3;

        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const float LINE_ALPHA = 0.8f;

        /// <summary>
        /// Builds the sprite and the prefab, registers both in the Fx group and wires the Game scene.
        /// </summary>
        [MenuItem("Coika/Setup Guide Line")]
        public static void Run()
        {
            var sprite = CreateDotSprite();
            if (sprite == null)
            {
                Debug.LogError("Setup Guide Line stopped: the dot sprite was not created.");
                return;
            }

            BuildPrefab(sprite);
            MakeAddressable(DotSpritePath);
            MakeAddressable(PrefabPath);
            WireScene();
            Debug.Log($"Guide line is up to date at {PrefabPath}.");
        }

        /// <summary>
        /// Writes the dot texture (one pixel wide, two pixels on and two off) and imports it point-sampled, without
        /// compression or mipmaps, at 16 pixels per unit, so it stays crisp when tiled (GDD §10).
        /// </summary>
        private static Sprite CreateDotSprite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DotSpritePath));
            var texture = new Texture2D(1, 4, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { Color.clear, Color.clear, Color.white, Color.white });
            texture.Apply();
            File.WriteAllBytes(DotSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(DotSpritePath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(DotSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TierDefinition.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.TopCenter;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(DotSpritePath);
        }

        /// <summary>
        /// Builds the prefab: the view on the root, a tiled line child using the dot sprite and a ghost child. The
        /// view sets the ghost sprite at runtime from the held piece.
        /// </summary>
        /// <param name="dot">The dot sprite of the line.</param>
        private static void BuildPrefab(Sprite dot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var root = new GameObject("GuideLine");
            try
            {
                var view = root.AddComponent<GuideLineView>();
                var line = CreateRenderer(root.transform, "Line", LINE_SORTING_ORDER);
                line.sprite = dot;
                line.drawMode = SpriteDrawMode.Tiled;
                line.color = new Color(1f, 1f, 1f, LINE_ALPHA);
                var ghost = CreateRenderer(root.transform, "Ghost", GHOST_SORTING_ORDER);

                var serialized = new SerializedObject(view);
                serialized.FindProperty("_line").objectReferenceValue = line;
                serialized.FindProperty("_ghost").objectReferenceValue = ghost;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Adds a disabled child with a sprite renderer, so nothing shows until the view decides to.
        /// </summary>
        private static SpriteRenderer CreateRenderer(Transform parent, string name, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.enabled = false;
            return spriteRenderer;
        }

        /// <summary>
        /// Registers an asset in the Fx Addressables group. Logs an error when the group is missing.
        /// </summary>
        /// <param name="path">Asset path.</param>
        private static void MakeAddressable(string path)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings != null ? settings.FindGroup(FxGroupName) : null;
            if (group == null)
            {
                Debug.LogError($"Addressables group '{FxGroupName}' not found; {path} was not made Addressable.");
                return;
            }

            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Opens the Game scene, points the installer at the prefab and saves the scene.
        /// </summary>
        private static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameSceneInstaller installer = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                installer = root.GetComponentInChildren<GameSceneInstaller>(true);
                if (installer != null)
                {
                    break;
                }
            }

            if (installer == null)
            {
                Debug.LogError($"Setup Guide Line stopped: no GameSceneInstaller in {ScenePath}.");
                return;
            }

            var serialized = new SerializedObject(installer);
            serialized.FindProperty("_guideLinePrefab.m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(PrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
