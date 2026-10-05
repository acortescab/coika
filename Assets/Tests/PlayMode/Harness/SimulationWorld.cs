using System;
using System.Collections.Generic;
using Coika.Data;
using Coika.Fx;
using Coika.Gameplay;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// The whole Game scene of the game, built in code for the simulation harness: jar, the 11 tiers with the GDD
    /// diameters and merge scores, piece factory, merge system, drop controller, overflow detector, score system,
    /// <see cref="RunSystems"/> and <see cref="GameManager"/>, wired like <c>GameSceneInstaller</c> does. It uses
    /// <see cref="TestAssetService"/> (no bundles, C-01) and owns the clock: the time of the world is the number of
    /// physics steps times the fixed step, so it advances inside a synchronous loop, where <c>Time.time</c> would
    /// stand still, and every system reads it through the injected clock.
    /// <para>
    /// A world is cheap and is never reused for a second simulation: a fresh pool and fresh bodies are what makes two
    /// runs with the same seed identical. The test owns the world and must call <see cref="Dispose"/>.
    /// </para>
    /// </summary>
    public sealed class SimulationWorld : IDisposable
    {
        private const float EVALUATION_TOLERANCE = 0.0005f;
        // More pieces than the jar can hold, so the pool never grows (and warns) during a long random run.
        private const int PREWARM_COUNT = 300;

        // Scene names must be unique while an earlier world's scene is still unloading.
        private static int sceneCounter;

        // Values of the GDD table (§3.2). Keep them in sync with the tier assets in Assets/Data/Tiers: the score test depends on them.
        private static readonly float[] TierDiameters = { 0.75f, 1.00f, 1.31f, 1.69f, 2.13f, 2.63f, 3.19f, 3.81f, 4.50f, 5.25f, 6.00f };
        private static readonly float[] TierMergeScores = { 1f, 3f, 6f, 10f, 15f, 21f, 28f, 36f, 45f, 55f, 66f };

        private readonly List<UnityEngine.Object> _created = new();
        private readonly SimulationMode2D _originalSimulationMode;
        private readonly Scene _scene;
        private readonly PhysicsScene2D _physics;
        private readonly float _fixedDeltaTime;
        private readonly int _firstSeed;

        private int _nextSeed;
        private long _steps;
        private float _overflowAccumulator;
        private bool _disposed;
        private FxDirector _fxDirector;

        /// <summary>
        /// Builds the world and pre-warms the piece pool. Nothing runs until <see cref="StartRun"/>.
        /// </summary>
        /// <param name="options">Seed and the rules the test bends.</param>
        /// <exception cref="InvalidOperationException">The pre-warm did not complete at once, so the asset service is not the harness double.</exception>
        public SimulationWorld(SimulationOptions options)
        {
            _originalSimulationMode = Physics2D.simulationMode;

            // A physics scene of its own gives every world a fresh Box2D world. The shared one keeps allocator and
            // broadphase state from the bodies of earlier worlds, which changes the order of contacts and makes two
            // runs with the same seed diverge.
            _scene = SceneManager.CreateScene("SimulationWorld-" + sceneCounter++, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            _physics = _scene.GetPhysicsScene2D();
            _fixedDeltaTime = Time.fixedDeltaTime;
            _firstSeed = options.Seed;
            _nextSeed = options.Seed;

            var pieceMaterial = new PhysicsMaterial2D("HarnessPiece") { friction = 0.4f, bounciness = 0.15f };
            var wallMaterial = new PhysicsMaterial2D("HarnessWall") { friction = 0.4f, bounciness = 0f };
            _created.Add(pieceMaterial);
            _created.Add(wallMaterial);

            Config = BuildConfig(options, pieceMaterial, wallMaterial);
            Tiers = BuildTiers();
            Animations = options.Animations;

            var jarObject = new GameObject("Jar");
            SceneManager.MoveGameObjectToScene(jarObject, _scene);
            _created.Add(jarObject);
            Jar = jarObject.AddComponent<Jar>();
            JarBuilder.Build(Jar, Config);

            var container = new GameObject("PieceContainer");
            SceneManager.MoveGameObjectToScene(container, _scene);
            _created.Add(container);
            Container = container.transform;
            Assets =new TestAssetService(_created, pieceMaterial);
            Factory = new PieceFactory(Assets, new AssetReference(), Config, container.transform, PREWARM_COUNT, () => Now);

            var prewarm = Factory.PrewarmAsync(Tiers);
            if (!prewarm.IsCompleted || prewarm.IsFaulted)
            {
                throw new InvalidOperationException("The harness asset service must complete the pre-warm at once: " + prewarm.Exception);
            }

            Input = new ScriptedDropInput();

            var mergeObject = new GameObject("MergeSystem");
            _created.Add(mergeObject);
            Merge = mergeObject.AddComponent<MergeSystem>();
            Merge.Initialize(Factory, Tiers, Config);

            if (options.Animations)
            {
                Ghosts = container.AddComponent<MergeGhostPool>();
                Ghosts.Initialize(Merge, Config.Feedback);
            }

            var overflowObject = new GameObject("OverflowDetector");
            _created.Add(overflowObject);
            Overflow = overflowObject.AddComponent<OverflowDetector>();
            Overflow.Initialize(Factory, Jar, Config);

            var controllerObject = new GameObject("DropController");
            _created.Add(controllerObject);
            Controller = controllerObject.AddComponent<DropController>();
            Controller.Initialize(Input, Jar, Factory, new SpawnQueue(Config, options.Seed), Tiers, Config);

            Score = new ScoreSystem(Config, Tiers, () => SimulatedSeconds);
            Systems = new RunSystems(Config, Tiers, Assets, Factory, Merge, Controller, Overflow, Score, () => SimulatedSeconds);
            Manager = new GameManager(Systems, () => _nextSeed);

            if (options.Particles)
            {
                Particles = TestParticleSpawner.Create(_created, Config.Feedback);
                SceneManager.MoveGameObjectToScene(Particles.gameObject, _scene);
                _fxDirector = new FxDirector(Particles, Config.Feedback, Tiers);
                _fxDirector.Bind(Merge, Score, Factory, new Vector2(0f, Jar.DangerLineY));
            }

            Controller.PieceDropped += HandlePieceDropped;
            Overflow.GameOverTriggered += Manager.EndRun;
        }

        /// <summary>The config with the GDD values and the changes of the options.</summary>
        public GameConfig Config { get; }

        /// <summary>The 11 tiers, in tier order, with the diameters and merge scores of the GDD.</summary>
        public IReadOnlyList<TierDefinition> Tiers { get; }

        /// <summary>The jar the pieces fall in.</summary>
        public Jar Jar { get; }

        /// <summary>The object that holds every pooled piece, active or not.</summary>
        public Transform Container { get; }

        /// <summary>The asset service double.</summary>
        public TestAssetService Assets { get; }

        /// <summary>The factory that creates the pieces, on the clock of the world.</summary>
        public PieceFactory Factory { get; }

        /// <summary>The scripted input the runner drives.</summary>
        public ScriptedDropInput Input { get; }

        /// <summary>The merge system.</summary>
        public MergeSystem Merge { get; }

        /// <summary>The overflow detector.</summary>
        public OverflowDetector Overflow { get; }

        /// <summary>The drop controller.</summary>
        public DropController Controller { get; }

        /// <summary>The score system.</summary>
        public ScoreSystem Score { get; }

        /// <summary>The systems of one run, as the installer builds them.</summary>
        public RunSystems Systems { get; }

        /// <summary>The state machine of the game.</summary>
        public GameManager Manager { get; }

        /// <summary>Whether the visual animations run in this world.</summary>
        public bool Animations { get; }

        /// <summary>The ghosts of the merged pieces, or null when the animations are off.</summary>
        public MergeGhostPool Ghosts { get; }

        /// <summary>The pooled particle spawner, or null when the particles are off.</summary>
        public ParticleSpawner Particles { get; }

        /// <summary>Simulated time in seconds: the steps taken times the fixed step. It is the clock of every system.</summary>
        public double SimulatedSeconds => _steps * (double)_fixedDeltaTime;

        /// <summary>Simulated time as the float the pieces and the detector use.</summary>
        public float Now => (float)SimulatedSeconds;

        /// <summary>Number of physics steps taken.</summary>
        public long Steps => _steps;

        /// <summary>The fixed step of every simulated step, taken from the project settings when the world was built.</summary>
        public float FixedDeltaTime => _fixedDeltaTime;

        /// <summary>Whether the game is over and nothing has restarted it yet.</summary>
        public bool IsGameOver => Manager.State == GameState.GameOver;

        /// <summary>Pieces dropped since the world was built, restarts included.</summary>
        public int PiecesDropped { get; private set; }

        /// <summary>Runs started after a game over.</summary>
        public int Restarts { get; private set; }

        /// <summary>Simulated seconds since the last drop.</summary>
        public float SecondsSinceLastDrop { get; private set; }

        /// <summary>
        /// Starts the first run with the seed of the options and takes the physics under script control.
        /// </summary>
        public void StartRun()
        {
            Manager.StartRun();
            UseScriptedPhysics();
            _overflowAccumulator = 0f;
        }

        /// <summary>
        /// Starts the next run after a game over with the next seed, through <see cref="GameManager.Retry"/>.
        /// </summary>
        public void Restart()
        {
            _nextSeed++;
            Manager.Retry();
            UseScriptedPhysics();
            _overflowAccumulator = 0f;
            Restarts++;
        }

        /// <summary>
        /// One physics step of the simulation, in the order the game does it: the held piece follows the input, the
        /// merge pass resolves the contacts of the previous step, the physics scene of the world advances, then the overflow detector
        /// (every 0.1 s), the combo and the drop controller run on the new time. Nothing here reads the real clock.
        /// </summary>
        public void Step()
        {
            var dt = _fixedDeltaTime;

            Controller.FixedTick(dt);
            Merge.ProcessQueue();
            _physics.Simulate(dt);
            _steps++;

            _overflowAccumulator += dt;
            if (_overflowAccumulator >= OverflowDetector.EVALUATION_INTERVAL - EVALUATION_TOLERANCE)
            {
                var elapsed = _overflowAccumulator;
                _overflowAccumulator = 0f;
                Overflow.Evaluate(elapsed, Now);
            }

            Systems.Tick();
            Controller.Tick(dt);
            Manager.Tick(dt);
            SecondsSinceLastDrop += dt;
            TickAnimations(dt);
        }

        /// <summary>
        /// Advances the visual animations of every active piece and of the merge ghosts. They never run by
        /// themselves here, because the harness steps synchronously and no frame passes.
        /// </summary>
        /// <param name="deltaTime">Seconds of the step.</param>
        private void TickAnimations(float deltaTime)
        {
            var pieces = Factory.ActivePieces;
            for (var i = 0; i < pieces.Count; i++)
            {
                var animator = pieces[i].Animator;
                if (animator != null)
                {
                    animator.Tick(deltaTime);
                }
            }

            if (Ghosts != null)
            {
                Ghosts.Tick(deltaTime);
            }
        }

        /// <summary>
        /// Counts the active pieces whose animator is still running or whose visual is not at identity scale.
        /// </summary>
        /// <returns>The number of pieces with an animation in progress or a stuck scale.</returns>
        public int CountPiecesAnimating()
        {
            var count = 0;
            var pieces = Factory.ActivePieces;
            for (var i = 0; i < pieces.Count; i++)
            {
                var animator = pieces[i].Animator;
                if (animator != null && (animator.IsRunning || animator.VisualScale != Vector3.one))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Describes the board and the score as they are now.
        /// </summary>
        /// <returns>A result with the pieces on the board, ordered by creation, without the held piece.</returns>
        public SimulationResult Snapshot()
        {
            var pieces = new List<Piece>();
            for (var i = 0; i < Factory.ActivePieces.Count; i++)
            {
                var piece = Factory.ActivePieces[i];
                if (!piece.IsHeld)
                {
                    pieces.Add(piece);
                }
            }

            pieces.Sort((a, b) => a.SequenceId.CompareTo(b.SequenceId));

            var snapshots = new List<PieceSnapshot>(pieces.Count);
            for (var i = 0; i < pieces.Count; i++)
            {
                var position = pieces[i].Rigidbody.position;
                snapshots.Add(new PieceSnapshot(pieces[i].Tier.Index, Mathf.RoundToInt(position.x * 100f), Mathf.RoundToInt(position.y * 100f)));
            }

            return new SimulationResult(
                _firstSeed,
                Score.Score,
                Score.HighestTierReached,
                Score.Merges,
                PiecesDropped,
                IsGameOver,
                Restarts,
                (long)Math.Round(SimulatedSeconds * 1000.0),
                snapshots);
        }

        /// <summary>
        /// Counts the pieces on the board, not the one held at the Drop Line.
        /// </summary>
        /// <returns>The number of active pieces that are not held.</returns>
        public int CountBoardPieces()
        {
            var count = 0;
            for (var i = 0; i < Factory.ActivePieces.Count; i++)
            {
                if (!Factory.ActivePieces[i].IsHeld)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Stops listening, gives the physics mode back and destroys everything the world created, at once, so a
        /// second world built in the same frame does not share the physics scene with the first.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Controller.PieceDropped -= HandlePieceDropped;
            Overflow.GameOverTriggered -= Manager.EndRun;
            _fxDirector?.Unbind();
            Systems.Dispose();
            Factory.Dispose();

            foreach (var created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            Physics2D.simulationMode = _originalSimulationMode;

            if (_scene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(_scene);
            }
        }

        /// <summary>
        /// Counts a drop and restarts the time since the last one.
        /// </summary>
        /// <param name="tier">Tier of the dropped piece.</param>
        private void HandlePieceDropped(int tier)
        {
            PiecesDropped++;
            SecondsSinceLastDrop = 0f;
        }

        /// <summary>
        /// A run turns the physics back to the player loop when it starts; the harness steps it by hand instead.
        /// </summary>
        private static void UseScriptedPhysics()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
        }

        /// <summary>
        /// Builds the config: the GDD defaults plus the changes of the options.
        /// </summary>
        /// <param name="options">The changes.</param>
        /// <param name="pieceMaterial">Material of the pieces.</param>
        /// <param name="wallMaterial">Material of the jar walls.</param>
        /// <returns>The config, registered for destruction.</returns>
        private GameConfig BuildConfig(SimulationOptions options, PhysicsMaterial2D pieceMaterial, PhysicsMaterial2D wallMaterial)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(config);
            TestReflection.SetField(config, "_pieceMaterial", pieceMaterial);
            TestReflection.SetField(config, "_wallMaterial", wallMaterial);

            if (options.JarSize.HasValue)
            {
                TestReflection.SetField(config, "_jarSize", options.JarSize.Value);
            }

            if (options.DropCooldown.HasValue)
            {
                TestReflection.SetField(config, "_dropDownCooldown", options.DropCooldown.Value);
            }

            if (options.ForcedOpening != null)
            {
                TestReflection.SetField(config, "_forcedOpeningTiers", options.ForcedOpening);
            }

            if (options.Animations || options.Particles)
            {
                var feedback = ScriptableObject.CreateInstance<FeedbackConfig>();
                _created.Add(feedback);
                TestReflection.SetField(config, "_feedback", feedback);
            }

            return config;
        }

        /// <summary>
        /// Builds the 11 tiers with the diameters and merge scores of the GDD (§3.2).
        /// </summary>
        /// <returns>The tiers, in tier order, registered for destruction.</returns>
        private IReadOnlyList<TierDefinition> BuildTiers()
        {
            return TestTiers.Build(_created, i => TierDiameters[i], i => TierMergeScores[i]);
        }
    }
}
