using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using UnityEngine;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR || DEBUG
using UnityEngine.InputSystem;
#endif

namespace Coika.UI
{
    /// <summary>
    /// Composition root of the Game scene (GDD §14.4) and the only place that uses scene references. It loads and
    /// preloads the assets through the asset service (C-01) behind a <see cref="LoadingOverlay"/>, builds the plain
    /// objects (<see cref="ScoreSystem"/>, <see cref="RunSystems"/>, <see cref="GameManager"/>, the presenters),
    /// hands them their dependencies through <c>Initialize</c>, connects the events, and starts the first run. It
    /// uses no singleton and no lookup by type; the optional save arrives from Boot through <see cref="UseSave"/>.
    /// <para>
    /// It lives in <c>Coika.UI</c> because the assembly rules (S-03) let only UI see both the gameplay and the views;
    /// the game rules stay in <see cref="GameManager"/>, which knows nothing of the scene.
    /// </para>
    /// <para>
    /// A failed load is logged and shows the Retry button of the overlay instead of an endless spinner; Retry
    /// releases what the failed attempt loaded and tries again. Everything it loaded, built or subscribed to is
    /// released when it is destroyed, even in the middle of a load. In the Editor and development builds, R restarts
    /// the run and K forces a game over.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/UI/Game Scene Installer")]
    [DisallowMultipleComponent]
    public class GameSceneInstaller : MonoBehaviour, ISaveConsumer
    {
        // Same-scene objects, so direct references are allowed (C-01).
        [SerializeField]
        private Jar _jar;
        [SerializeField]
        private DropController _dropController;
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
        [SerializeField]
        private AssetReference _canvasPrefab;

        private bool _destroyed;

        private IAssetService _assets;
        private LoadingOverlay _overlay;
        private GameConfig _loadedConfig;
        private ThemeDefinition _theme;
        private IReadOnlyList<TierDefinition> _tiers;
        private Transform _container;
        private PieceFactory _factory;
        private GameObject _loadedCanvasPrefab;
        private GameObject _canvas;
        private HudView _hud;
        private GameOverView _gameOver;
        private TierSpriteCache _sprites;
        private ScoreSystem _score;
        private RunSystems _systems;
        private GameManager _manager;
        private HudPresenter _hudPresenter;
        private GameOverPresenter _gameOverPresenter;
        private Action _onGameOverTriggered;
        private Action<RunContext> _onRunStarted;
        private Action<RunSummary> _onGameOverReady;
        private Action _onRetryRequested;
        private Action<RunSummary> _onRunEnded;
        private SaveSystem _save;
        private Action _onOverlayRetry;
        private bool _subscribed;
        private bool _loading;

        /// <summary>The manager of the run, or null until the assets are loaded.</summary>
        public GameManager Manager => _manager;

        /// <summary>
        /// Receives the save system from the Boot installer. It must arrive before the objects are built.
        /// </summary>
        /// <param name="save">The loaded save system.</param>
        public void UseSave(SaveSystem save)
        {
            _save = save;
        }

        /// <summary>The loading overlay, for tests.</summary>
        public LoadingOverlay Overlay => _overlay;

        /// <summary>
        /// Sets the frame rate (GDD §14.6) and caches the event handlers, so subscribing allocates nothing.
        /// </summary>
        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            _onGameOverTriggered = HandleGameOverTriggered;
            _onRunStarted = HandleRunStarted;
            _onGameOverReady = HandleGameOverReady;
            _onRunEnded = HandleRunEnded;
            _onRetryRequested = HandleRetryRequested;
            _onOverlayRetry = HandleOverlayRetry;

            _overlay = new LoadingOverlay();
        }

        /// <summary>
        /// Starts listening again when the component is re-enabled after the run was built (S-23).
        /// </summary>
        private void OnEnable()
        {
            _overlay.RetryClicked += _onOverlayRetry;
            Subscribe();
        }

        /// <summary>
        /// Stops listening (S-23).
        /// </summary>
        private void OnDisable()
        {
            _overlay.RetryClicked -= _onOverlayRetry;
            Unsubscribe();
        }

        /// <summary>
        /// Starts loading and building.
        /// </summary>
        private void Start()
        {
            Observe(LoadAndStartAsync());
        }

        /// <summary>
        /// Advances the wait before the Game Over view, and reads the debug keys.
        /// </summary>
        private void Update()
        {
            _manager?.Tick(Time.unscaledDeltaTime);

#if UNITY_EDITOR || DEBUG
            ReadDebugKeys();
#endif
        }

        /// <summary>
        /// Ticks the combo on the same clock the merges use (see <see cref="ScoreSystem"/>).
        /// </summary>
        private void FixedUpdate()
        {
            _systems?.Tick();
        }

        /// <summary>
        /// Releases everything, even in the middle of a load.
        /// </summary>
        private void OnDestroy()
        {
            _destroyed = true;
            TearDown();
            _overlay?.Dispose();
            _overlay = null;
        }

        /// <summary>
        /// Loads and builds everything and starts the first run; on failure it shows the Retry button and waits.
        /// </summary>
        private async Task LoadAndStartAsync()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            _overlay.ShowLoading();
            try
            {
                await BuildAsync();
                if (_destroyed)
                {
                    // What arrived after OnDestroy ran was assigned to the fields, so release it now.
                    TearDown();
                    return;
                }

                _overlay.Hide();
                _manager.StartRun();
            }
            catch (ObjectDisposedException) when (_destroyed)
            {
                // Leaving Play mode or unloading the scene during the pre-warm disposes the factory: expected.
            }
            catch (Exception e)
            {
                TearDown();
                if (!_destroyed)
                {
                    Debug.LogError($"GameSceneInstaller could not load the game: {e.Message}", this);
                    _overlay.ShowFailure();
                }
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// Loads the assets in order and builds the objects. After every wait it stops if the scene went away, and
        /// whatever was loaded is released by <see cref="TearDown"/>.
        /// </summary>
        private async Task BuildAsync()
        {
            if (_jar == null || _dropController == null || _input == null || _mergeSystem == null || _overflowDetector == null
                || _config == null || !_config.RuntimeKeyIsValid() || _piecePrefab == null || !_piecePrefab.RuntimeKeyIsValid()
                || _canvasPrefab == null || !_canvasPrefab.RuntimeKeyIsValid())
            {
                throw new InvalidOperationException("The installer needs the Jar, the DropController, the PointerInputReader, the MergeSystem, the OverflowDetector, the GameConfig, the Piece prefab and the GameCanvas prefab.");
            }

            _assets ??= new AssetService();

            _loadedConfig = await _assets.LoadAsset<GameConfig>(_config);
            if (_destroyed)
            {
                return;
            }

            _theme = await _assets.LoadAsset<ThemeDefinition>(_loadedConfig.Theme);
            if (_destroyed)
            {
                return;
            }

            // Kept in a local too: if the scene goes away while the tiers load, TearDown has already forgotten
            // the theme, and the tiers that arrive afterwards still need to be released.
            var theme = _theme;
            var tiers = await theme.LoadTiersAsync(_assets);
            if (_destroyed)
            {
                theme.ReleaseTiers(_assets, tiers);
                return;
            }

            _tiers = tiers;

            _container = new GameObject("Pieces").transform;
            _factory = new PieceFactory(_assets, _piecePrefab, _loadedConfig, _container);
            await _factory.PrewarmAsync(_tiers);
            if (_destroyed)
            {
                return;
            }

            var prefab = await _assets.LoadAsset<GameObject>(_canvasPrefab);
            _loadedCanvasPrefab = prefab;
            if (_destroyed)
            {
                return;
            }

            _canvas = Instantiate(prefab);
            _hud = _canvas.GetComponentInChildren<HudView>(true);
            _gameOver = _canvas.GetComponentInChildren<GameOverView>(true);
            if (_hud == null || _gameOver == null)
            {
                throw new InvalidOperationException("The GameCanvas prefab needs a HudView and a GameOverView.");
            }

            _sprites = new TierSpriteCache(_assets);
            await _sprites.LoadAsync(_tiers);
            if (_destroyed)
            {
                return;
            }

            Compose();
        }

        /// <summary>
        /// Builds the plain objects and gives every system its dependencies.
        /// </summary>
        private void Compose()
        {
            // The controller needs a queue to be initialized; the run replaces it with its own.
            var firstQueue = new SpawnQueue(_loadedConfig, Environment.TickCount);
            _mergeSystem.Initialize(_factory, _tiers, _loadedConfig);
            _dropController.Initialize(_input, _jar, _factory, firstQueue, _tiers, _loadedConfig);
            _overflowDetector.Initialize(_factory, _jar, _loadedConfig);

            _score = new ScoreSystem(_loadedConfig, _tiers, () => Time.timeAsDouble);

            // The Boot installer hands the save over through UseSave; without it (tests) nothing is persisted.
            if (_save != null)
            {
                _score.BestScore = _save.Data.bestScore.classic;
            }

            _systems = new RunSystems(_loadedConfig, _tiers, _assets, _factory, _mergeSystem, _dropController, _overflowDetector, _score);
            _manager = new GameManager(_systems, () => Environment.TickCount);

            _hudPresenter = new HudPresenter(_hud, _score, _sprites.Get);
            _gameOverPresenter = new GameOverPresenter(_gameOver, _sprites.Get);
            Subscribe();
        }

        /// <summary>
        /// Connects the events of the built objects, once.
        /// </summary>
        private void Subscribe()
        {
            if (_subscribed || _manager == null || !isActiveAndEnabled)
            {
                return;
            }

            _subscribed = true;
            _overflowDetector.GameOverTriggered += _onGameOverTriggered;
            _manager.RunStarted += _onRunStarted;
            _manager.GameOverReady += _onGameOverReady;
            _manager.RunEnded += _onRunEnded;
            _gameOverPresenter.RetryRequested += _onRetryRequested;
        }

        /// <summary>
        /// Disconnects the events, if connected.
        /// </summary>
        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            _overflowDetector.GameOverTriggered -= _onGameOverTriggered;
            _manager.RunStarted -= _onRunStarted;
            _manager.GameOverReady -= _onGameOverReady;
            _manager.RunEnded -= _onRunEnded;
            _gameOverPresenter.RetryRequested -= _onRetryRequested;
        }

        /// <summary>
        /// Ends the run when the detector reports the overflow. The manager ignores a second call.
        /// </summary>
        private void HandleGameOverTriggered()
        {
            _manager.EndRun();
        }

        /// <summary>
        /// Hides the Game Over view of the previous run and shows the new one on the HUD.
        /// </summary>
        private void HandleRunStarted(RunContext run)
        {
            _gameOver.Hide();
            _hudPresenter.BindQueue(run.Queue);
            _hudPresenter.Refresh();
        }

        /// <summary>
        /// Folds the finished run into the save and requests a write (GDD §13).
        /// </summary>
        private void HandleRunEnded(RunSummary summary)
        {
            if (_save == null)
            {
                return;
            }

            _save.Data.RecordRun(summary.Score, summary.HighestTier, summary.Merges, summary.DurationSeconds);
            _save.RequestSave();
        }

        /// <summary>
        /// Shows the Game Over view, after the delay of the manager.
        /// </summary>
        private void HandleGameOverReady(RunSummary summary)
        {
            _gameOverPresenter.Present(summary);
        }

        /// <summary>
        /// Starts a new run when the player clicks Retry.
        /// </summary>
        private void HandleRetryRequested()
        {
            _manager.Retry();
        }

        /// <summary>
        /// Tries the loading again after a failure.
        /// </summary>
        private void HandleOverlayRetry()
        {
            Observe(LoadAndStartAsync());
        }

        /// <summary>
        /// Awaits a task started from a Unity message or an event handler, so a failure is logged instead of lost.
        /// </summary>
        private async void Observe(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Debug.LogError($"GameSceneInstaller failed: {e.Message}", this);
            }
        }

        /// <summary>
        /// Stops and releases everything built and loaded so far, in reverse order. Safe to call more than once and
        /// with parts missing, so a failed attempt can be retried from nothing.
        /// </summary>
        private void TearDown()
        {
            Unsubscribe();

            _hudPresenter?.Dispose();
            _gameOverPresenter?.Dispose();
            _hudPresenter = null;
            _gameOverPresenter = null;

            _systems?.Dispose();
            _systems = null;
            _manager = null;
            _score = null;

            if (_dropController != null)
            {
                _dropController.Disable();
            }

            if (_overflowDetector != null)
            {
                _overflowDetector.Disable();
            }

            if (_mergeSystem != null)
            {
                _mergeSystem.enabled = false;
            }

            _sprites?.Dispose();
            _sprites = null;

            if (_canvas != null)
            {
                Destroy(_canvas);
            }

            _canvas = null;
            _hud = null;
            _gameOver = null;

            if (_assets != null && _loadedCanvasPrefab != null)
            {
                _assets.ReleaseAsset(_loadedCanvasPrefab);
            }

            _loadedCanvasPrefab = null;

            _factory?.Dispose();
            _factory = null;

            if (_assets != null && _theme != null && _tiers != null)
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

#if UNITY_EDITOR || DEBUG
        /// <summary>
        /// Debug helpers: R restarts the run, K forces a game over.
        /// </summary>
        private void ReadDebugKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _manager == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                _manager.StartRun();
            }
            else if (keyboard.kKey.wasPressedThisFrame)
            {
                _manager.EndRun();
            }
        }
#endif
    }
}
