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
        private const string SpritesFolder = "Assets/Art/Sprites/Jar";

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

                if (!JarBuilder.Build(jar, config))
                {
                    Debug.LogError("The jar could not be built, so the prefab was not saved. See the error above.");
                    return;
                }

                AssignSprites(jar);

                // Assigning a sprite resets the size of a tiled sprite renderer to the sprite's own size, so the
                // builder runs again to put the jar sizes back. It is idempotent.
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

        /// <summary>
        /// Gives the floor, the two walls and the Danger Line their placeholder sprites when they have none.
        /// Sprites are art, so they are assigned here and not by the runtime <see cref="JarBuilder"/>, which
        /// never loads assets.
        /// </summary>
        /// <param name="jar">The jar whose visuals receive the sprites.</param>
        private static void AssignSprites(Jar jar)
        {
            AssignSprite(jar.transform.Find(JarBuilder.FLOOR_NAME), "Jar_Floor");
            AssignSprite(jar.transform.Find(JarBuilder.LEFT_WALL_NAME), "Jar_WallLeft");
            AssignSprite(jar.transform.Find(JarBuilder.RIGHT_WALL_NAME), "Jar_WallRight");
            AssignSprite(jar.DangerLine != null ? jar.DangerLine.transform : null, "Jar_DangerLine");
        }

        /// <summary>
        /// Assigns a sprite from the jar sprites folder to the sprite renderer of an object, unless it already has
        /// one. Logs an error when the sprite is not imported yet.
        /// </summary>
        /// <param name="target">The object with the sprite renderer. A null target is ignored.</param>
        /// <param name="spriteName">File name of the sprite, without extension.</param>
        private static void AssignSprite(Transform target, string spriteName)
        {
            if (target == null || !target.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                return;

            if (spriteRenderer.sprite != null)
                return;

            var path = $"{SpritesFolder}/{spriteName}.png";
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spriteRenderer.sprite == null)
                Debug.LogError($"Sprite not found at {path}. Let Unity import it and run the tool again.");
        }
    }
}
