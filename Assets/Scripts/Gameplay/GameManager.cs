using System;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Owns the <see cref="GameState"/> and coordinates the run lifecycle: start, end, retry (GDD §7, §14.3). It
    /// holds no gameplay rule (no scoring, no overflow): it only tells the <see cref="IRunSystems"/> when to prepare,
    /// play and stop, and announces the result. It is a plain object, so the transitions are tested without a scene.
    /// <para>
    /// An illegal call is rejected with a log and changes nothing. <see cref="StartRun"/> is legal in every state, so
    /// calling it while playing restarts the run cleanly; <see cref="EndRun"/> twice is a no-op, which also covers two
    /// pieces overflowing in the same step.
    /// </para>
    /// <para>
    /// After <see cref="EndRun"/> the Game Over view is not shown at once: the owner calls <see cref="Tick"/> every
    /// frame with the unscaled delta time, and after <see cref="GAME_OVER_DELAY"/> seconds the manager raises
    /// <see cref="GameOverReady"/>. Unscaled, so it does not depend on how the physics is frozen. Retry never reloads
    /// a scene or an asset: it prepares a new run with the loaded content.
    /// </para>
    /// </summary>
    public sealed class GameManager
    {
        /// <summary>Seconds between the game over and the Game Over view.</summary>
        public const float GAME_OVER_DELAY = 1.2f;

        private readonly IRunSystems _systems;
        private readonly Func<int> _seedSource;

        private RunSummary _summary;
        private float _delayLeft;
        private bool _waitingForView;

        /// <summary>
        /// Creates a manager in <see cref="GameState.Boot"/>.
        /// </summary>
        /// <param name="systems">The gameplay systems the manager drives.</param>
        /// <param name="seedSource">Gives the seed of a new run; time-based for the Classic mode.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public GameManager(IRunSystems systems, Func<int> seedSource)
        {
            _systems = systems ?? throw new ArgumentNullException(nameof(systems));
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            State = GameState.Boot;
        }

        /// <summary>Raised after every state change, with the old and the new state.</summary>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>Raised when a run is ready and playing, with what the UI needs to show it.</summary>
        public event Action<RunContext> RunStarted;

        /// <summary>Raised at the moment the run ends, with its summary.</summary>
        public event Action<RunSummary> RunEnded;

        /// <summary>Raised once, <see cref="GAME_OVER_DELAY"/> seconds after <see cref="RunEnded"/>, to show the Game Over view.</summary>
        public event Action<RunSummary> GameOverReady;

        /// <summary>Current state.</summary>
        public GameState State { get; private set; }

        /// <summary>
        /// Starts a fresh run from any state, restarting the run when one is in progress.
        /// </summary>
        public void StartRun()
        {
            _waitingForView = false;
            var context = _systems.PrepareRun(_seedSource());
            _systems.BeginPlaying();
            SetState(GameState.Playing);
            RunStarted?.Invoke(context);
        }

        /// <summary>
        /// Ends the run in progress. It does nothing when the game is already over, and is rejected with a warning
        /// when no run is in progress.
        /// </summary>
        public void EndRun()
        {
            if (State == GameState.GameOver)
            {
                return;
            }

            if (!GameStateTransitions.IsAllowed(State, GameState.GameOver))
            {
                Debug.LogWarning($"EndRun ignored: there is no run to end in state {State}.");
                return;
            }

            _summary = _systems.StopPlaying();
            _delayLeft = GAME_OVER_DELAY;
            _waitingForView = true;
            SetState(GameState.GameOver);
            RunEnded?.Invoke(_summary);
        }

        /// <summary>
        /// Starts a new run from the Game Over state, without reloading the scene or the assets. Rejected with a
        /// warning in any other state.
        /// </summary>
        public void Retry()
        {
            if (State != GameState.GameOver)
            {
                Debug.LogWarning($"Retry ignored: it is only valid in state {GameState.GameOver}, not {State}.");
                return;
            }

            StartRun();
        }

        /// <summary>
        /// Advances the wait before the Game Over view. Call it every frame.
        /// </summary>
        /// <param name="unscaledDeltaTime">Seconds since the last call, not affected by the time scale.</param>
        public void Tick(float unscaledDeltaTime)
        {
            if (!_waitingForView)
            {
                return;
            }

            _delayLeft -= unscaledDeltaTime;
            if (_delayLeft > 0f)
            {
                return;
            }

            _waitingForView = false;
            GameOverReady?.Invoke(_summary);
        }

        /// <summary>
        /// Changes the state and announces it.
        /// </summary>
        private void SetState(GameState next)
        {
            var previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
