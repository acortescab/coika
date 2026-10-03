using System;
using System.Collections.Generic;
using Coika.Core;
using Coika.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Gameplay
{
    /// <summary>
    /// TEMPORARY composition root of the Game scene (TODO #11: the game manager replaces it). Nothing composes the
    /// gameplay objects yet, so this component loads the config, the theme and the Piece prefab through the asset
    /// service (C-01), builds the <see cref="PieceFactory"/> and a <see cref="SpawnQueue"/>, and hands them to the
    /// <see cref="DropController"/>, which it then enables. That makes the drop playable in the Game scene before
    /// the manager exists.
    /// <para>
    /// Errors are logged and never thrown, because the loading is asynchronous. It releases everything it loaded
    /// when it is destroyed, even if that happens while the assets are still loading.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/Drop Controller Bootstrap")]
    [DisallowMultipleComponent]
    public class DropControllerBootstrap : MonoBehaviour
    {
        // Same-scene objects, so direct references are allowed (C-01).
        [SerializeField]
        private Jar _jar;
        [SerializeField]
        private DropController _controller;
        [SerializeField]
        private PointerInputReader _input;
        [SerializeField]
        private MergeSystem _mergeSystem;
        [SerializeField]
        private OverflowDetector _overflowDetector;
        // Addressable assets, loaded through the asset service (C-01).
        [SerializeField]
        private AssetReferenceT<GameConfig> _config;
        [SerializeField]
        private AssetReference _piecePrefab;

        private IAssetService _assets;
        private GameConfig _loadedConfig;
        private ThemeDefinition _theme;
        private IReadOnlyList<TierDefinition> _tiers;
        private PieceFactory _factory;
        private Transform _container;
        private bool _destroyed;

        /// <summary>
        /// Builds everything and enables the controller. A failure is logged, not thrown.
        /// </summary>
        private async void Start()
        {
            try
            {
                await SetUpAsync();
            }
            catch (ObjectDisposedException)
            {
                // Leaving Play mode or unloading the scene during the pre-warm disposes the factory: expected.
            }
            catch (Exception e)
            {
                Debug.LogError($"DropControllerBootstrap could not start: {e.Message}", this);
            }
        }

        /// <summary>
        /// Releases everything this component loaded and built.    
        /// </summary>
        private void OnDestroy()
        {
            _destroyed = true;
            CleanUp();
        }

        /// <summary>
        /// Loads the assets in order, builds the factory and the queue, and starts the controller. After every
        /// wait it checks that the component still exists, and cleans up if it does not.
        /// </summary>
        private async System.Threading.Tasks.Task SetUpAsync()
        {
            if (_jar == null || _controller == null || _input == null || _mergeSystem == null || _overflowDetector == null || _config == null || !_config.RuntimeKeyIsValid() || !_piecePrefab.RuntimeKeyIsValid())
            {
                Debug.LogError("DropControllerBootstrap needs the Jar, the DropController, the PointerInputReader, the MergeSystem, the OverflowDetector, the GameConfig and the Piece prefab.", this);
                return;
            }

            _assets = new AssetService();

            _loadedConfig = await _assets.LoadAsset<GameConfig>(_config);
            if (_destroyed)
            {
                CleanUp();
                return;
            }

            _theme = await _assets.LoadAsset<ThemeDefinition>(_loadedConfig.Theme);
            if (_destroyed)
            {
                CleanUp();
                return;
            }

            // The theme is kept in a local: if this component is destroyed while the tiers load, CleanUp has
            // already forgotten the field, and the tiers that arrive afterwards still need to be released.
            var theme = _theme;
            var tiers = await theme.LoadTiersAsync(_assets);
            if (_destroyed)
            {
                theme.ReleaseTiers(_assets, tiers);
                CleanUp();
                return;
            }

            _tiers = tiers;

            _container = new GameObject("Pieces").transform;
            _factory = new PieceFactory(_assets, _piecePrefab, _loadedConfig, _container);
            await _factory.PrewarmAsync(_tiers);
            if (_destroyed)
            {
                CleanUp();
                return;
            }

            // A time-based seed: the game manager will choose the seed of a run (Classic: time, Daily: the date).
            var queue = new SpawnQueue(_loadedConfig, Environment.TickCount);
            _mergeSystem.Initialize(_factory, _tiers, _loadedConfig);
            _controller.Initialize(_input, _jar, _factory, queue, _tiers, _loadedConfig);
            _controller.Enable();

            // TODO #10/#11: the game manager listens to GameOverTriggered; until then the detector only drives the line.
            _overflowDetector.Initialize(_factory, _jar, _loadedConfig);
            _overflowDetector.Enable();
        }

        /// <summary>
        /// Gives back what was loaded and disposes what was built. Safe to call more than once and with parts
        /// missing.
        /// </summary>
        private void CleanUp()
        {
            if (_controller != null)
            {
                _controller.Disable();
            }

            if (_mergeSystem != null)
            {
                _mergeSystem.enabled = false;
            }

            if (_overflowDetector != null)
            {
                _overflowDetector.Disable();
            }

            _factory?.Dispose();
            _factory = null;

            if (_theme != null && _tiers != null)
            {
                _theme.ReleaseTiers(_assets, _tiers);
            }

            _tiers = null;

            if (_assets != null)
            {
                _assets.ReleaseAsset(_theme);
                _assets.ReleaseAsset(_loadedConfig);
            }

            _theme = null;
            _loadedConfig = null;

            if (_container != null)
            {
                Destroy(_container.gameObject);
            }

            _container = null;
        }
    }
}
