using Coika.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Runs the whole setup of issue #10 in order (localization, then the canvas prefab) and points the
    /// <see cref="GameSceneInstaller"/> of the Game scene at the Addressable canvas
    /// prefab. Idempotent: running it again updates what exists.
    /// </summary>
    public static class GameUiSetup
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";

        /// <summary>
        /// Sets up the localization, the canvas prefab and the scene wiring. Stops at the first step that fails.
        /// </summary>
        [MenuItem("Coika/Setup Game UI")]
        public static void Run()
        {
            LocalizationSetup.Run();
            GameCanvasPrefabTool.Run();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(GameCanvasPrefabTool.PrefabPath) == null)
            {
                Debug.LogError("Setup Game UI stopped: the canvas prefab was not created.");
                return;
            }

            WireScene();
        }

        /// <summary>
        /// Opens the Game scene, points the installer at the Addressable canvas prefab, and saves the scene. The
        /// installer itself and its scene references are part of the scene.
        /// </summary>
        private static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var installer = Object.FindAnyObjectByType<GameSceneInstaller>();
            if (installer == null)
            {
                Debug.LogError($"Setup Game UI stopped: no GameSceneInstaller in {ScenePath}.");
                return;
            }

            var serialized = new SerializedObject(installer);
            serialized.FindProperty("_canvasPrefab.m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(GameCanvasPrefabTool.PrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Game UI is wired in {ScenePath}.");
        }
    }
}
