using Coika.Data;
using Coika.Gameplay;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Creates or updates the Piece prefab and makes it Addressable in the Core-Data group (C-01). The prefab
    /// is authored here, with the values of issue #4, because they never change at runtime; the per-tier values
    /// (sprite, radius, mass) are set by the piece when it is initialized.
    /// </summary>
    public static class PiecePrefabTool
    {
        public const string PrefabPath = "Assets/Prefabs/Piece.prefab";

        /// <summary>Sorting order of a piece: above the jar walls (1) and below the Danger Line (10).</summary>
        public const int SORTING_ORDER = 2;

        /// <summary>Linear damping of the piece body.</summary>
        public const float LINEAR_DAMPING = 0.1f;

        /// <summary>Angular damping of the piece body.</summary>
        public const float ANGULAR_DAMPING = 0.3f;

        /// <summary>Radius the prefab starts with, in world units. A piece sets its own radius per tier when initialized.</summary>
        public const float DEFAULT_RADIUS = 0.5f;

        private const string GameConfigPath = "Assets/Data/GameConfig/GameConfig.asset";

        /// <summary>
        /// Builds the prefab and registers it in the Core-Data Addressables group. Aborts with an error, saving
        /// nothing, when the GameConfig, its piece material or the Piece layer is missing.
        /// </summary>
        [MenuItem("Coika/Build Piece Prefab")]
        public static void Run()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null || config.PieceMaterial == null)
            {
                Debug.LogError($"GameConfig with a piece material not found at {GameConfigPath}.");
                return;
            }

            var layer = LayerMask.NameToLayer(Piece.LAYER_NAME);
            if (layer < 0)
            {
                Debug.LogError($"Physics layer '{Piece.LAYER_NAME}' is not defined.");
                return;
            }

            var prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var root = prefabExists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Piece");

            try
            {
                Configure(root, layer, config.PieceMaterial);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (prefabExists)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    Object.DestroyImmediate(root);
            }

            MakeAddressable();
            Debug.Log($"Piece prefab is up to date at {PrefabPath}.");
        }

        /// <summary>
        /// Gives the piece object the components and values the prefab needs: a dynamic body with continuous
        /// detection, interpolation and damping, a circle collider with the piece material, the sprite renderer
        /// and the Piece component. The mass follows the area of the collider.
        /// </summary>
        /// <param name="root">The piece object.</param>
        /// <param name="layer">Index of the Piece physics layer.</param>
        /// <param name="material">Physics material of the collider.</param>
        private static void Configure(GameObject root, int layer, PhysicsMaterial2D material)
        {
            root.layer = layer;

            var spriteRenderer = GetOrAdd<SpriteRenderer>(root);
            spriteRenderer.sortingOrder = SORTING_ORDER;

            var collider = GetOrAdd<CircleCollider2D>(root);
            collider.radius = DEFAULT_RADIUS;
            collider.sharedMaterial = material;

            var body = GetOrAdd<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = LINEAR_DAMPING;
            body.angularDamping = ANGULAR_DAMPING;
            body.useAutoMass = false;
            body.mass = Mathf.PI * DEFAULT_RADIUS * DEFAULT_RADIUS;

            GetOrAdd<Piece>(root);
        }

        /// <summary>
        /// Returns the component of the given type on the object, adding it when it is not there yet.
        /// </summary>
        /// <typeparam name="T">Type of the component.</typeparam>
        /// <param name="target">The object that holds the component.</param>
        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            if (!target.TryGetComponent<T>(out var component))
                component = target.AddComponent<T>();

            return component;
        }

        /// <summary>
        /// Registers the prefab in the Core-Data Addressables group. Logs an error when the group is missing.
        /// </summary>
        private static void MakeAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings != null ? settings.FindGroup(TierDataSetup.DataGroupName) : null;
            if (group == null)
            {
                Debug.LogError($"Addressables group '{TierDataSetup.DataGroupName}' not found; the Piece prefab was not made Addressable.");
                return;
            }

            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), group, false, false);
            AssetDatabase.SaveAssets();
        }
    }
}
