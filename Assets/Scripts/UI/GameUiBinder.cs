using System;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Gameplay;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.UI
{
    /// <summary>
    /// TEMPORARY wiring of the UI in the Game scene (TODO(#11): the game manager replaces it). It loads the
    /// <c>GameCanvas</c> prefab through the asset service (C-01), and when the <see cref="DropControllerBootstrap"/>
    /// announces the run it builds the sprite cache and the presenters, so the HUD and the Game Over view show it.
    /// <para>
    /// The canvas and the run arrive in any order, so each handler stores what it got and the wiring runs once both
    /// are there. Errors are logged, never thrown, because the loading is asynchronous. Everything it loaded or
    /// subscribed to is released when it is destroyed, even while the loading is still running.
    /// </para>
    /// <para>
    /// It wires one run: a second <see cref="DropControllerBootstrap.RunStarted"/> is ignored. The Retry click
    /// reaches <see cref="GameOverPresenter.RetryRequested"/> and goes no further until issue #11.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/UI/Game UI Binder")]
    [DisallowMultipleComponent]
    public class GameUiBinder : MonoBehaviour
    {
        // Same-scene object, so a direct reference is allowed (C-01).
        [SerializeField]
        private DropControllerBootstrap _bootstrap;
        // Addressable prefab, loaded through the asset service (C-01).
        [SerializeField]
        private AssetReference _canvasPrefab;

        private IAssetService _assets;
        private GameObject _loadedPrefab;
        private GameObject _canvas;
        private HudView _hud;
        private GameOverView _gameOver;
        private RunContext _run;
        private RunSummary? _summary;
        private TierSpriteCache _sprites;
        private HudPresenter _hudPresenter;
        private GameOverPresenter _gameOverPresenter;
        private Action<RunContext> _onRunStarted;
        private Action<RunSummary> _onRunEnded;
        private bool _wiring;
        private bool _destroyed;

        /// <summary>
        /// Caches the event handlers, so subscribing allocates nothing.
        /// </summary>
        private void Awake()
        {
            _onRunStarted = HandleRunStarted;
            _onRunEnded = HandleRunEnded;
        }

        /// <summary>
        /// Starts listening to the run announcements of the bootstrap (S-23).
        /// </summary>
        private void OnEnable()
        {
            if (_bootstrap == null)
            {
                return;
            }

            _bootstrap.RunStarted += _onRunStarted;
            _bootstrap.RunEnded += _onRunEnded;
        }

        /// <summary>
        /// Stops listening to the bootstrap (S-23).
        /// </summary>
        private void OnDisable()
        {
            if (_bootstrap == null)
            {
                return;
            }

            _bootstrap.RunStarted -= _onRunStarted;
            _bootstrap.RunEnded -= _onRunEnded;
        }

        /// <summary>
        /// Starts loading the canvas. A failure is logged, not thrown.
        /// </summary>
        private void Start()
        {
            Observe(LoadCanvasAsync(), "load the canvas");
        }

        /// <summary>
        /// Releases the presenters, the sprites, the canvas and the prefab handle.
        /// </summary>
        private void OnDestroy()
        {
            _destroyed = true;
            _hudPresenter?.Dispose();
            _gameOverPresenter?.Dispose();
            _sprites?.Dispose();
            _hudPresenter = null;
            _gameOverPresenter = null;
            _sprites = null;

            if (_canvas != null)
            {
                Destroy(_canvas);
            }

            if (_assets != null && _loadedPrefab != null)
            {
                _assets.ReleaseAsset(_loadedPrefab);
            }

            _loadedPrefab = null;
        }

        /// <summary>
        /// Loads and instantiates the canvas, finds its two views, and wires the run if it already started.
        /// </summary>
        private async Task LoadCanvasAsync()
        {
            if (_bootstrap == null || _canvasPrefab == null || !_canvasPrefab.RuntimeKeyIsValid())
            {
                Debug.LogError("GameUiBinder needs the DropControllerBootstrap and the GameCanvas prefab.", this);
                return;
            }

            _assets = new AssetService();
            var prefab = await _assets.LoadAsset<GameObject>(_canvasPrefab);
            if (_destroyed)
            {
                _assets.ReleaseAsset(prefab);
                return;
            }

            _loadedPrefab = prefab;
            _canvas = Instantiate(prefab);
            _hud = _canvas.GetComponentInChildren<HudView>(true);
            _gameOver = _canvas.GetComponentInChildren<GameOverView>(true);
            if (_hud == null || _gameOver == null)
            {
                Debug.LogError("The GameCanvas prefab needs a HudView and a GameOverView.", this);
                return;
            }

            // The run may have started before the canvas was ready, and nothing announces it a second time.
            if (_run == null)
            {
                _run = _bootstrap.CurrentRun;
            }

            await TryWireAsync();
        }

        /// <summary>
        /// Remembers the run and wires the UI if the canvas is ready.
        /// </summary>
        private void HandleRunStarted(RunContext run)
        {
            _run = run;
            Observe(TryWireAsync(), "wire the UI");
        }

        /// <summary>
        /// Remembers the summary and shows it, or shows it later when the wiring is done.
        /// </summary>
        private void HandleRunEnded(RunSummary summary)
        {
            _summary = summary;
            _gameOverPresenter?.Present(summary);
        }

        /// <summary>
        /// Awaits a task started from a Unity message or an event handler, so a failure is logged with its
        /// context instead of being lost in an unobserved task.
        /// </summary>
        /// <param name="task">The running task.</param>
        /// <param name="what">What the task does, for the error message.</param>
        private async void Observe(Task task, string what)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Debug.LogError($"GameUiBinder could not {what}: {e.Message}", this);
            }
        }

        /// <summary>
        /// Builds the sprite cache and the presenters once the canvas and the run are both available. When the
        /// sprites fail to load, the partial cache is released and the error is rethrown to be logged, so no
        /// half-built state stays behind.
        /// </summary>
        private async Task TryWireAsync()
        {
            if (_wiring || _hudPresenter != null || _run == null || _hud == null || _gameOver == null)
            {
                return;
            }

            _wiring = true;
            var run = _run;
            var sprites = new TierSpriteCache(run.Assets);
            _sprites = sprites;
            try
            {
                await sprites.LoadAsync(run.Tiers);
            }
            catch
            {
                sprites.Dispose();
                if (_sprites == sprites)
                {
                    _sprites = null;
                }

                throw;
            }
            finally
            {
                _wiring = false;
            }

            if (_destroyed)
            {
                sprites.Dispose();
                return;
            }

            _hudPresenter = new HudPresenter(_hud, run.Score, sprites.Get);
            _hudPresenter.BindQueue(run.Queue);

            _gameOverPresenter = new GameOverPresenter(_gameOver, sprites.Get);
            if (_summary.HasValue)
            {
                _gameOverPresenter.Present(_summary.Value);
            }
        }
    }
}
