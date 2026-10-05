using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Fx;
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
    /// uses no singleton and no lookup by type; the optional save and settings arrive from Boot through
    /// <see cref="UseSave"/> and <see cref="UseSettings"/>.
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
    public class GameSceneInstaller : MonoBehaviour, ISaveConsumer, ISettingsConsumer, IAudioConsumer, IHapticsConsumer
    {
        private const float GAME_OVER_DUCK_DB = -6f; // GDD §11: music ducks 6 dB on game over
        private const float GAME_OVER_DUCK_SECONDS = 0.5f;
        private const float UNDUCK_SECONDS = 0.3f;
        private const float RESUME_INPUT_GRACE_SECONDS = 0.15f; // input ignored just after a resume (issue #35)

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
        [SerializeField]
        private AssetReference _guideLinePrefab;
        // Optional: without it the game simply shows no particles.
        [SerializeField]
        private AssetReference _fxPrefab;
        // Optional: without it the game simply shows no screen flash.
        [SerializeField]
        private AssetReference _screenFlashPrefab;
        // Optional: the parent of the camera, so the shake never touches the framing of the camera itself.
        [SerializeField]
        private ScreenShake _cameraShake;

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
        private GameObject _loadedGuideLinePrefab;
        private GameObject _guideLine;
        private GuideLineView _guideLineView;
        private GameObject _loadedFxPrefab;
        private GameObject _fx;
        private ParticleSpawner _particles;
        private FeedbackDirector _feedbackDirector;
        private GameObject _loadedFlashPrefab;
        private GameObject _flashObject;
        private ScreenFlash _screenFlash;
        private TimeScaleOwner _timeScale;
        private HudView _hud;
        private GameOverView _gameOver;
        private PauseView _pauseView;
        private ConfirmView _confirmView;
        private PausePresenter _pausePresenter;
        private Action _onPauseClicked;
        private Action _onBackPressed;
        private Action _onResumeRequested;
        private Action<ConfirmKind> _onConfirmed;
        private TierSpriteCache _sprites;
        private ScoreSystem _score;
        private RunSystems _systems;
        private GameManager _manager;
        private HudPresenter _hudPresenter;
        private GameOverPresenter _gameOverPresenter;
        private Action _onGameOverTriggered;
        private Action<RunContext> _onRunStarted;
        private MergeGhostPool _ghosts;
        private Action<RunSummary> _onGameOverReady;
        private Action _onRetryRequested;
        private Action<RunSummary> _onRunEnded;
        private SaveSystem _save;
        private SettingsService _settings;
        private IAudioService _audio;
        private IHaptics _haptics;
        private SoundBank _soundBank;
        private Action<GameState, GameState> _onStateChanged;
        private Action<SettingsChanged> _onSettingsChanged;
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

        /// <summary>
        /// Receives the settings from the Boot installer. It must arrive before the objects are built.
        /// </summary>
        /// <param name="settings">The shared settings service.</param>
        public void UseSettings(SettingsService settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Receives the audio service from the Boot installer. Without it (tests) the scene is silent.
        /// </summary>
        /// <param name="audio">The shared audio service.</param>
        public void UseAudio(IAudioService audio)
        {
            _audio = audio;
        }

        /// <summary>
        /// Receives the haptics service from the Boot installer. Without it (tests) the scene has no haptics. The
        /// <see cref="FeedbackDirector"/> plays them; it is only built when the game config has a feedback config, so
        /// without one the scene has no sounds and no haptics of the game events either.
        /// </summary>
        /// <param name="haptics">The shared haptics service.</param>
        public void UseHaptics(IHaptics haptics)
        {
            _haptics = haptics;
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
            _onSettingsChanged = HandleSettingsChanged;
            _onRunStarted = HandleRunStarted;
            _onGameOverReady = HandleGameOverReady;
            _onRunEnded = HandleRunEnded;
            _onStateChanged = HandleStateChanged;
            _onRetryRequested = HandleRetryRequested;
            _onPauseClicked = HandlePauseClicked;
            _onBackPressed = HandleBackPressed;
            _onResumeRequested = HandleResumeRequested;
            _onConfirmed = HandleConfirmed;
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
            _timeScale?.Tick();
            _feedbackDirector?.Tick();

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
                || _canvasPrefab == null || !_canvasPrefab.RuntimeKeyIsValid()
                || _guideLinePrefab == null || !_guideLinePrefab.RuntimeKeyIsValid())
            {
                throw new InvalidOperationException("The installer needs the Jar, the DropController, the PointerInputReader, the MergeSystem, the OverflowDetector, the GameConfig, the Piece prefab, the GameCanvas prefab and the GuideLine prefab.");
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
            _pauseView = _canvas.GetComponentInChildren<PauseView>(true);
            _confirmView = _canvas.GetComponentInChildren<ConfirmView>(true);
            if (_hud == null || _gameOver == null || _pauseView == null || _confirmView == null)
            {
                throw new InvalidOperationException("The GameCanvas prefab needs a HudView, a GameOverView, a PauseView and a ConfirmView.");
            }

            _sprites = new TierSpriteCache(_assets);
            await _sprites.LoadAsync(_tiers);
            if (_destroyed)
            {
                return;
            }

            // The guide line comes with the rest of the load phase, never on demand (C-01).
            var guideLinePrefab = await _assets.LoadAsset<GameObject>(_guideLinePrefab);
            _loadedGuideLinePrefab = guideLinePrefab;
            if (_destroyed)
            {
                return;
            }

            _guideLine = Instantiate(guideLinePrefab);
            _guideLineView = _guideLine.GetComponent<GuideLineView>();
            if (_guideLineView == null)
            {
                throw new InvalidOperationException("The GuideLine prefab needs a GuideLineView.");
            }

            // The particle systems come with the load phase and are prewarmed in Compose, never during play (C-01).
            if (_loadedConfig.Feedback != null && _fxPrefab != null && _fxPrefab.RuntimeKeyIsValid())
            {
                var fxPrefab = await _assets.LoadAsset<GameObject>(_fxPrefab);
                _loadedFxPrefab = fxPrefab;
                if (_destroyed)
                {
                    return;
                }

                _fx = Instantiate(fxPrefab);
                _particles = _fx.GetComponent<ParticleSpawner>();
                if (_particles == null)
                {
                    throw new InvalidOperationException("The ParticleSpawner prefab needs a ParticleSpawner.");
                }
            }

            // The flash overlay comes with the load phase, and is reused for every flash (C-01).
            if (_loadedConfig.Feedback != null && _screenFlashPrefab != null && _screenFlashPrefab.RuntimeKeyIsValid())
            {
                var flashPrefab = await _assets.LoadAsset<GameObject>(_screenFlashPrefab);
                _loadedFlashPrefab = flashPrefab;
                if (_destroyed)
                {
                    return;
                }

                _flashObject = Instantiate(flashPrefab);
                _screenFlash = _flashObject.GetComponent<ScreenFlash>();
                if (_screenFlash == null)
                {
                    throw new InvalidOperationException("The ScreenFlash prefab needs a ScreenFlash.");
                }
            }

            // The clips come with the rest of the load phase, never during play (C-01).
            if (_audio != null)
            {
                _soundBank = new SoundBank(_assets, SoundBank.SFX_LABEL, SoundBank.MUSIC_GAMEPLAY_LABEL);
                await _soundBank.LoadAsync();
                if (_destroyed)
                {
                    return;
                }

                _audio.AddBank(_soundBank);
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
            ApplyFingerOffset();
            _guideLineView.Initialize(_dropController, _jar, _tiers);
            ApplyGuideLine();
            _overflowDetector.Initialize(_factory, _jar, _loadedConfig);

            // The ghosts are purely visual: without a feedback config the merge simply shows no shrink.
            if (_loadedConfig.Feedback != null)
            {
                _ghosts = _container.gameObject.AddComponent<MergeGhostPool>();
                _ghosts.Initialize(_mergeSystem, _loadedConfig.Feedback);
            }

            _score = new ScoreSystem(_loadedConfig, _tiers, () => Time.timeAsDouble);

            if (_particles != null)
            {
                _particles.Initialize(_loadedConfig.Feedback);
                ApplyReduceMotion();
            }

            // The Boot installer hands the save over through UseSave; without it (tests) nothing is persisted.
            if (_save != null)
            {
                _score.BestScore = _save.Data.bestScore.classic;
            }

            _systems = new RunSystems(_loadedConfig, _tiers, _assets, _factory, _mergeSystem, _dropController, _overflowDetector, _score);
            _manager = new GameManager(_systems, () => Environment.TickCount);

            // Not part of the optional feedback: the pause must freeze the game even without a feedback config.
            _timeScale = new TimeScaleOwner(new UnityTimeScale(), () => Time.unscaledTimeAsDouble);

            // After the score system, so the combo is already updated when a merge is played.
            ComposeFeedback();

            _hudPresenter = new HudPresenter(_hud, _score, _sprites.Get);
            _gameOverPresenter = new GameOverPresenter(_gameOver, _sprites.Get);
            _pausePresenter = new PausePresenter(_pauseView, _confirmView, _audio);
            Subscribe();
        }

        /// <summary>
        /// Builds the feedback: the shake of the camera rig and the flash overlay, all on the
        /// unscaled clock, and the director that maps every gameplay event to them, to the particles, the sounds
        /// and the haptics. The parts without an object here do nothing.
        /// </summary>
        private void ComposeFeedback()
        {
            var feedback = _loadedConfig.Feedback;
            if (feedback == null)
            {
                return;
            }

            Func<double> unscaledClock = () => Time.unscaledTimeAsDouble;

            // Explicit checks: a destroyed MonoBehaviour must not be touched, and `?.` does not see it as null.
            IScreenShake shake = NullScreenEffects.Instance;
            if (_cameraShake != null)
            {
                _cameraShake.Initialize(feedback, unscaledClock);
                shake = _cameraShake;
            }

            IScreenFlash flash = NullScreenEffects.Instance;
            if (_screenFlash != null)
            {
                _screenFlash.Initialize(feedback, unscaledClock);
                flash = _screenFlash;
            }

            IParticleSpawner particles = _particles != null ? _particles : NullParticleSpawner.Instance;
            _feedbackDirector = new FeedbackDirector(
                _audio, _haptics, particles, shake, _timeScale, flash, feedback, _tiers, unscaledClock);
            _feedbackDirector.Bind(_mergeSystem, _score, _dropController, _overflowDetector, _factory, _manager, _jar);
            ApplyReduceMotion();
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
            _manager.StateChanged += _onStateChanged;
            _gameOverPresenter.RetryRequested += _onRetryRequested;
            _hud.PauseClicked += _onPauseClicked;
            _input.BackPressed += _onBackPressed;
            _pausePresenter.ResumeRequested += _onResumeRequested;
            _pausePresenter.Confirmed += _onConfirmed;
            if (_settings != null)
            {
                _settings.Changed += _onSettingsChanged;
            }

            // A setting may have changed while this component was disabled and not listening.
            ApplyFingerOffset();
            ApplyGuideLine();
        }

        /// <summary>
        /// Gives the guide line view the Guide Line setting (on without Boot, as in tests).
        /// </summary>
        private void ApplyGuideLine()
        {
            if (_guideLineView != null)
            {
                _guideLineView.SetSettingOn(_settings == null || _settings.GuideLine);
            }
        }

        /// <summary>
        /// Gives the particle spawner, the screen effects and the Danger Line the Reduce Shake setting (off without
        /// Boot, as in tests): fewer particles, no shake, no slow-mo and a soft 2 Hz pulse.
        /// </summary>
        private void ApplyReduceMotion()
        {
            var reduce = _settings != null && _settings.ReduceShake;
            if (_particles != null)
            {
                _particles.ReduceMotion = reduce;
            }

            if (_feedbackDirector != null)
            {
                _feedbackDirector.ReduceShake = reduce;
            }

            if (_jar != null && _jar.DangerLine != null)
            {
                _jar.DangerLine.SetPulseRate(reduce ? DangerLine.PulseRate.Soft : DangerLine.PulseRate.Fast);
            }
        }

        /// <summary>
        /// Gives the reader the Finger Offset and Left-handed settings (defaults without Boot, as in tests) and the
        /// distance of <see cref="GameConfig.FingerOffset"/>.
        /// </summary>
        private void ApplyFingerOffset()
        {
            var fingerOffset = _settings != null && _settings.FingerOffset;
            var leftHanded = _settings != null && _settings.LeftHanded;
            _input.ConfigureFingerOffset(fingerOffset, leftHanded, _loadedConfig.FingerOffset);
        }

        /// <summary>
        /// Applies a changed setting that the input depends on while the game runs.
        /// </summary>
        /// <param name="change">The setting that changed.</param>
        private void HandleSettingsChanged(SettingsChanged change)
        {
            if (change.Key == SettingKey.FingerOffset || change.Key == SettingKey.LeftHanded)
            {
                ApplyFingerOffset();
            }
            else if (change.Key == SettingKey.GuideLine)
            {
                ApplyGuideLine();
            }
            else if (change.Key == SettingKey.ReduceShake)
            {
                ApplyReduceMotion();
            }
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
            _manager.StateChanged -= _onStateChanged;
            _gameOverPresenter.RetryRequested -= _onRetryRequested;
            _hud.PauseClicked -= _onPauseClicked;
            _input.BackPressed -= _onBackPressed;
            _pausePresenter.ResumeRequested -= _onResumeRequested;
            _pausePresenter.Confirmed -= _onConfirmed;
            if (_settings != null)
            {
                _settings.Changed -= _onSettingsChanged;
            }
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
            _ghosts?.ResetAll();
            _particles?.ResetAll();
            ApplyReduceMotion();
            _hudPresenter.BindQueue(run.Queue);
            _hudPresenter.Refresh();

            // Starts the loop with the first run and brings the music back after a game over (retry).
            _audio?.DuckMusic(0f, UNDUCK_SECONDS);
            _audio?.PlayMusic(MusicId.Gameplay);
        }

        /// <summary>
        /// Pauses the music and the time with the game and resumes them afterwards, and ends a slow-mo when the game
        /// is over. The <see cref="FeedbackDirector"/> stops the shake and the flash on pause by itself.
        /// </summary>
        /// <param name="previous">The state that was left.</param>
        /// <param name="next">The state that was entered.</param>
        private void HandleStateChanged(GameState previous, GameState next)
        {
            if (_timeScale != null)
            {
                _timeScale.Paused = next == GameState.Paused;
                if (next == GameState.GameOver)
                {
                    _timeScale.Cancel();
                }
            }

            if (next == GameState.Paused)
            {
                // Forgets the press in progress and ignores input for a moment after the resume, so the tap on
                // Resume never drops a piece. The grace counts game time, which only runs once resumed.
                _dropController.BlockInput(RESUME_INPUT_GRACE_SECONDS);
                _pausePresenter.Open();
                _audio?.PauseMusic();
            }
            else if (previous == GameState.Paused)
            {
                _pausePresenter.Close();
                _audio?.ResumeMusic();
            }
        }

        /// <summary>
        /// Pauses the game when the player taps the pause button.
        /// </summary>
        private void HandlePauseClicked()
        {
            _audio?.PlaySfx(SfxId.UiClick);
            PauseIfPlaying();
        }

        /// <summary>
        /// The Back button: opens the pause menu while playing, and closes the top panel of the pause flow (or
        /// resumes) while paused. It does nothing in the other states.
        /// </summary>
        private void HandleBackPressed()
        {
            if (_manager.State == GameState.Paused)
            {
                _pausePresenter.HandleBack();
            }
            else
            {
                PauseIfPlaying();
            }
        }

        /// <summary>
        /// Continues the run when the player asks to. A resume never happens by itself.
        /// </summary>
        private void HandleResumeRequested()
        {
            _manager.Resume();
        }

        /// <summary>
        /// Acts on a confirmed dialog: Restart starts a fresh run with the loaded assets. Menu waits for the Menu
        /// scene (M3); its button is disabled until then, so it cannot be reached yet.
        /// </summary>
        /// <param name="kind">What the dialog asked.</param>
        private void HandleConfirmed(ConfirmKind kind)
        {
            if (kind == ConfirmKind.Restart)
            {
                _manager.StartRun();
            }
        }

        /// <summary>
        /// Pauses the game and writes the save when the app is interrupted (a call, the app switcher, a lost
        /// window). Does nothing unless a run is in progress, and never resumes by itself. Public so tests can
        /// raise the interruption without a device.
        /// </summary>
        public void PauseForInterruption()
        {
            if (_manager == null)
            {
                return;
            }

            PauseIfPlaying();
            _save?.Save();
        }

        /// <summary>
        /// Pauses when a run is being played, and ignores any other state without a warning.
        /// </summary>
        private void PauseIfPlaying()
        {
            if (_manager != null && _manager.State == GameState.Playing)
            {
                _manager.Pause();
            }
        }

        /// <summary>
        /// The app goes to the background (mobile): pauses the run.
        /// </summary>
        /// <param name="paused">True when the app is pausing.</param>
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                PauseForInterruption();
            }
        }

        /// <summary>
        /// The window loses focus (desktop, split screen): pauses the run.
        /// </summary>
        /// <param name="hasFocus">Whether the window has focus now.</param>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                PauseForInterruption();
            }
        }

        /// <summary>
        /// Ducks the music and folds the finished run into the save and requests a write (GDD §13).
        /// </summary>
        private void HandleRunEnded(RunSummary summary)
        {
            _audio?.DuckMusic(GAME_OVER_DUCK_DB, GAME_OVER_DUCK_SECONDS);

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
            _pausePresenter?.Dispose();
            _hudPresenter = null;
            _gameOverPresenter = null;
            _pausePresenter = null;

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

            if (_soundBank != null)
            {
                _audio?.RemoveBank(_soundBank);
                _soundBank.Dispose();
                _soundBank = null;
            }

            _sprites?.Dispose();
            _sprites = null;

            if (_guideLine != null)
            {
                Destroy(_guideLine);
            }

            _guideLine = null;
            _guideLineView = null;

            if (_assets != null && _loadedGuideLinePrefab != null)
            {
                _assets.ReleaseAsset(_loadedGuideLinePrefab);
            }

            _loadedGuideLinePrefab = null;

            _feedbackDirector?.Unbind();
            _feedbackDirector = null;
            if (_timeScale != null)
            {
                // Leaves the scale at 1 for whatever loads next, even when torn down while paused.
                _timeScale.Paused = false;
                _timeScale.Cancel();
            }

            _timeScale = null;
            // Explicit checks: a destroyed MonoBehaviour must not be touched, and `?.` does not see it as null.
            if (_cameraShake != null)
            {
                _cameraShake.Clear();
            }

            if (_flashObject != null)
            {
                Destroy(_flashObject);
            }

            _flashObject = null;
            _screenFlash = null;

            if (_assets != null && _loadedFlashPrefab != null)
            {
                _assets.ReleaseAsset(_loadedFlashPrefab);
            }

            _loadedFlashPrefab = null;

            if (_fx != null)
            {
                Destroy(_fx);
            }

            _fx = null;
            _particles = null;

            if (_assets != null && _loadedFxPrefab != null)
            {
                _assets.ReleaseAsset(_loadedFxPrefab);
            }

            _loadedFxPrefab = null;

            if (_canvas != null)
            {
                Destroy(_canvas);
            }

            _canvas = null;
            _hud = null;
            _gameOver = null;
            _pauseView = null;
            _confirmView = null;

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
