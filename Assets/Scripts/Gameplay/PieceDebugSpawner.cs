#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using UnityEditor;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Debug helper to validate the piece physics before the drop controller exists: in Play mode, click in the
    /// Game view to spawn the selected tier at the cursor. Up and Down arrows (or the mouse wheel) change the
    /// tier, and the Tier Index field can be edited in the Inspector while playing.
    /// <para>
    /// Editor only: everything is inside <c>#if UNITY_EDITOR</c>, so in a player build this component does
    /// nothing. It has no asset references, so it does not pull anything into the scene bundle (C-01): it finds
    /// the config, the prefab and the tiers through the AssetDatabase, and builds a real <see cref="PieceFactory"/>
    /// with the real asset service. Remove it from the scene before release.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/Debug/Piece Spawner")]
    [DisallowMultipleComponent]
    public class PieceDebugSpawner : MonoBehaviour
    {
#if UNITY_EDITOR
        private const string GameConfigPath = "Assets/Data/GameConfig/GameConfig.asset";
        private const string PiecePrefabPath = "Assets/Prefabs/Piece/Piece.prefab";
        private const string TiersFolder = "Assets/Data/Tiers";

        [SerializeField, Min(0)]
        private int _tierIndex;

        private PieceFactory _factory;
        private Transform _container;
        private List<TierDefinition> _tiers;
        private Camera _camera;
        private Jar _jar;

        /// <summary>Whether the factory finished pre-warming, so clicks can spawn pieces.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Index of the tier that the next click spawns.</summary>
        public int TierIndex => _tierIndex;

        /// <summary>
        /// Builds the factory with the real asset service and pre-warms it. Errors are logged, not thrown, because
        /// this is a debug tool and must never break the scene.
        /// </summary>
        private async void Start()
        {
            try
            {
                await SetUpAsync();
            }
            catch (ObjectDisposedException)
            {
                // Leaving Play mode or unloading the scene during the pre-warm disposes the factory: expected, not an error.
            }
            catch (Exception e)
            {
                Debug.LogError($"PieceDebugSpawner could not start: {e.Message}", this);
            }
        }

        /// <summary>
        /// Disposes the factory, which destroys the spawned pieces and releases the loaded assets, and removes the
        /// container it created.
        /// </summary>
        private void OnDestroy()
        {
            _factory?.Dispose();

            if (_container != null)
                Destroy(_container.gameObject);
        }

        /// <summary>
        /// Reads the arrow keys and the mouse wheel to change the tier, and spawns a piece on a left click.
        /// </summary>
        private void Update()
        {
            if (!IsReady)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame)
                    SelectTier(_tierIndex + 1);

                if (keyboard.downArrowKey.wasPressedThisFrame)
                    SelectTier(_tierIndex - 1);
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0f)
                SelectTier(_tierIndex + 1);
            else if (wheel < 0f)
                SelectTier(_tierIndex - 1);

            if (mouse.leftButton.wasPressedThisFrame)
                Spawn(mouse.position.ReadValue());
        }

        /// <summary>
        /// Chooses the tier that the next click spawns and logs it. The index is kept inside the tier range.
        /// </summary>
        /// <param name="index">Tier index, from 0 (smallest) up.</param>
        public void SelectTier(int index)
        {
            if (_tiers == null || _tiers.Count == 0)
                return;

            _tierIndex = Mathf.Clamp(index, 0, _tiers.Count - 1);
            Debug.Log($"Piece spawner: tier {_tierIndex} ({_tiers[_tierIndex].DisplayName}).", this);
        }

        /// <summary>
        /// Spawns the selected tier at a screen position, with no velocity. The position is kept inside the jar
        /// interior when there is a jar, so a piece is never created inside a wall.
        /// </summary>
        /// <param name="screenPosition">Position in screen pixels, such as the mouse position.</param>
        /// <returns>The spawned piece, or null when the spawner is not ready.</returns>
        public Piece Spawn(Vector2 screenPosition)
        {
            if (!IsReady || _camera == null)
                return null;

            var tier = _tiers[Mathf.Clamp(_tierIndex, 0, _tiers.Count - 1)];
            var world = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(_camera.transform.position.z)));
            Vector2 position = world;

            if (_jar != null)
                position = ClampInsideJar(position, tier.DiameterUnits * 0.5f, _jar.InteriorMin, _jar.InteriorMax);

            return _factory.Create(tier, position, Vector2.zero);
        }

        /// <summary>
        /// Moves a position so a piece of the given radius centred there is inside the jar interior and above the
        /// floor. If the piece is wider than the interior it is centred.
        /// </summary>
        /// <param name="position">Wanted centre of the piece, in world units.</param>
        /// <param name="radius">Radius of the piece.</param>
        /// <param name="interiorMin">Bottom-left corner of the jar interior.</param>
        /// <param name="interiorMax">Top-right corner of the jar interior.</param>
        /// <returns>The adjusted position.</returns>
        public static Vector2 ClampInsideJar(Vector2 position, float radius, Vector2 interiorMin, Vector2 interiorMax)
        {
            var minX = interiorMin.x + radius;
            var maxX = interiorMax.x - radius;
            var x = minX > maxX ? (interiorMin.x + interiorMax.x) * 0.5f : Mathf.Clamp(position.x, minX, maxX);
            var y = Mathf.Max(position.y, interiorMin.y + radius);
            return new Vector2(x, y);
        }

        /// <summary>
        /// Finds the config, the prefab and the tiers in the Editor, then builds and pre-warms the factory.
        /// </summary>
        private async Task SetUpAsync()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            var prefabGuid = AssetDatabase.AssetPathToGUID(PiecePrefabPath);
            _tiers = AssetDatabase.FindAssets("t:TierDefinition", new[] { TiersFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TierDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(tier => tier.Index)
                .ToList();

            _camera = Camera.main;
            if (config == null || string.IsNullOrEmpty(prefabGuid) || _tiers.Count == 0 || _camera == null)
            {
                Debug.LogError("PieceDebugSpawner needs the GameConfig, the Piece prefab, the tiers and a Main Camera.", this);
                return;
            }

            _jar = FindAnyObjectByType<Jar>();
            _container = new GameObject("DebugPieces").transform;
            _factory = new PieceFactory(new AssetService(), new AssetReference(prefabGuid), config, _container);

            await _factory.PrewarmAsync(_tiers);

            _tierIndex = Mathf.Clamp(_tierIndex, 0, _tiers.Count - 1);
            IsReady = true;
            Debug.Log($"Piece spawner ready: click to spawn, Up/Down or the mouse wheel change the tier (now {_tierIndex}, {_tiers[_tierIndex].DisplayName}).", this);
        }
#endif
    }
}
