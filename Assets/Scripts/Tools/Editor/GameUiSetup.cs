using Coika.Gameplay;
using Coika.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Runs the whole setup of issue #10 in order (localization, then the canvas prefab) and adds the
    /// <see cref="GameUiBinder"/> to the Game scene, pointing it at the bootstrap and at the Addressable canvas
    /// prefab. Idempotent: running it again updates what exists and adds nothing twice.
    /// </summary>
    public static class GameUiSetup
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const string BinderObjectName = "GameUi";

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
        /// Opens the Game scene, adds or updates the binder, and saves the scene.
        /// </summary>
        private static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var bootstrap = Object.FindFirstObjectByType<DropControllerBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError($"Setup Game UI stopped: no DropControllerBootstrap in {ScenePath}.");
                return;
            }

            var binder = Object.FindFirstObjectByType<GameUiBinder>();
            if (binder == null)
            {
                binder = new GameObject(BinderObjectName).AddComponent<GameUiBinder>();
            }

            var serialized = new SerializedObject(binder);
            serialized.FindProperty("_bootstrap").objectReferenceValue = bootstrap;
            serialized.FindProperty("_canvasPrefab.m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(GameCanvasPrefabTool.PrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Game UI is wired in {ScenePath}.");
        }
    }
}
