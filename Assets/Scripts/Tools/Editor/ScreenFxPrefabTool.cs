using System.IO;
using Coika.Fx;
using Coika.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Coika.Tools
{
    /// <summary>
    /// Creates or updates the screen effects of issue #33: the ScreenFlash overlay prefab (made Addressable in the Fx
    /// group, C-01) and, in the Game scene, the camera rig that the <see cref="ScreenShake"/> moves. The main camera
    /// becomes a child of the rig, which sits at the origin, and the <see cref="GameSceneInstaller"/> is pointed at
    /// both. Idempotent.
    /// </summary>
    public static class ScreenFxPrefabTool
    {
        /// <summary>Path of the ScreenFlash prefab.</summary>
        public const string PREFAB_PATH = "Assets/Prefabs/Fx/ScreenFlash.prefab";

        /// <summary>Sorting order of the overlay canvas: above every other canvas.</summary>
        public const int SORTING_ORDER = 100;

        /// <summary>Name of the camera rig object.</summary>
        public const string RIG_NAME = "CameraRig";

        private const string SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const string CAMERA_NAME = "Main Camera";

        /// <summary>
        /// Builds the prefab, registers it in the Fx group and wires the Game scene.
        /// </summary>
        [MenuItem("Coika/Setup Screen Effects")]
        public static void Run()
        {
            BuildPrefab();
            MakeAddressable(PREFAB_PATH);
            WireScene();
            Debug.Log($"Screen effects are up to date at {PREFAB_PATH}.");
        }

        /// <summary>
        /// Builds the prefab: an overlay canvas with a canvas group and one white image over the whole screen that
        /// does not catch input.
        /// </summary>
        private static void BuildPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PREFAB_PATH));
            var root = new GameObject("ScreenFlash", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SORTING_ORDER;
                var group = root.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
                var flash = root.AddComponent<ScreenFlash>();

                var imageObject = new GameObject("Flash", typeof(RectTransform));
                imageObject.transform.SetParent(root.transform, false);
                var rect = (RectTransform)imageObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var image = imageObject.AddComponent<Image>();
                image.color = Color.white;
                image.raycastTarget = false;

                var serialized = new SerializedObject(flash);
                serialized.FindProperty("_group").objectReferenceValue = group;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Registers an asset in the Fx Addressables group. Logs an error when the group is missing.
        /// </summary>
        /// <param name="path">Asset path.</param>
        private static void MakeAddressable(string path)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings != null ? settings.FindGroup(FxPrefabTool.FX_GROUP_NAME) : null;
            if (group == null)
            {
                Debug.LogError($"Addressables group '{FxPrefabTool.FX_GROUP_NAME}' not found; {path} was not made Addressable.");
                return;
            }

            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Opens the Game scene, puts the main camera under a rig at the origin, points the installer at the rig and
        /// the prefab, and saves the scene.
        /// </summary>
        private static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            GameSceneInstaller installer = null;
            GameObject camera = null;
            GameObject rig = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                installer = installer != null ? installer : root.GetComponentInChildren<GameSceneInstaller>(true);
                if (root.name == CAMERA_NAME)
                {
                    camera = root;
                }
                else if (root.name == RIG_NAME)
                {
                    rig = root;
                }
            }

            if (installer == null)
            {
                Debug.LogError($"Setup Screen Effects stopped: no GameSceneInstaller in {SCENE_PATH}.");
                return;
            }

            if (rig == null)
            {
                if (camera == null)
                {
                    Debug.LogError($"Setup Screen Effects stopped: no root '{CAMERA_NAME}' in {SCENE_PATH}.");
                    return;
                }

                rig = new GameObject(RIG_NAME);
                rig.transform.position = Vector3.zero;
                camera.transform.SetParent(rig.transform, true);
            }

            if (!rig.TryGetComponent<ScreenShake>(out var shake))
            {
                shake = rig.AddComponent<ScreenShake>();
            }

            var serialized = new SerializedObject(installer);
            serialized.FindProperty("_cameraShake").objectReferenceValue = shake;
            serialized.FindProperty("_screenFlashPrefab.m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(PREFAB_PATH);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
