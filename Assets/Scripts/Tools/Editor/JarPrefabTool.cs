using Coika.Data;
using Coika.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Creates or updates the Jar prefab from the GameConfig asset. Run it again after changing the jar values
    /// in GameConfig: the colliders and the exposed bounds are resized consistently.
    /// </summary>
    public static class JarPrefabTool
    {
        public const string PrefabPath = "Assets/Prefabs/Jar.prefab";
        private const string GameConfigPath = "Assets/Data/GameConfig/GameConfig.asset";

        /// <summary>
        /// Builds the jar with <see cref="JarBuilder"/> and saves it as the Jar prefab, creating the prefab when
        /// it does not exist yet. Aborts with an error when the GameConfig asset is missing.
        /// </summary>
        [MenuItem("Coika/Build Jar Prefab")]
        public static void Run()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                Debug.LogError($"GameConfig not found at {GameConfigPath}.");
                return;
            }

            var prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = prefabExists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Jar");

            try
            {
                if (!root.TryGetComponent<Jar>(out var jar))
                    jar = root.AddComponent<Jar>();

                JarBuilder.Build(jar, config);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (prefabExists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }

            Debug.Log($"Jar prefab is up to date at {PrefabPath}.");
        }
    }
}
