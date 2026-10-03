using System;
using System.Collections.Generic;
using Coika.Core;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The live gameplay systems of the Game scene behind <see cref="IRunSystems"/>. It knows the order in which they
    /// are reset, enabled and stopped, so a run starts from nothing and nothing of the previous run survives: the
    /// pieces are released (never destroyed), the queue is replaced, and the score, the combo, the merges and the
    /// overflow detector go back to their starting values. It keeps one <see cref="ScoreSystem"/> for the whole scene,
    /// bound once, so the HUD listens to the same object in every run.
    /// <para>
    /// The physics is frozen at game over with <c>Physics2D.simulationMode = Script</c> and restored to
    /// <c>FixedUpdate</c> when a run is prepared. That freezes only the physics: the time scale stays 1, so the
    /// unscaled fades of the Game Over view and the game over wait keep running, and a pause (M2) is free to use the
    /// time scale. Nothing here reloads a scene.
    /// </para>
    /// </summary>
    public sealed class RunSystems : IRunSystems, IDisposable
    {
        private readonly GameConfig _config;
        private readonly IReadOnlyList<TierDefinition> _tiers;
        private readonly IAssetService _assets;
        private readonly PieceFactory _factory;
        private readonly MergeSystem _merge;
        private readonly DropController _controller;
        private readonly OverflowDetector _overflow;
        private readonly RunTimer _timer;

        /// <summary>
        /// Creates the systems of the scene and binds the score to the merges and the drops. The controller, the
        /// merge system and the detector must already be initialized.
        /// </summary>
        /// <param name="config">Source of the spawn settings.</param>
        /// <param name="tiers">Every tier, in tier order.</param>
        /// <param name="assets">Asset service the UI loads the tier sprites with.</param>
        /// <param name="factory">Pool of the pieces in play.</param>
        /// <param name="merge">Merges the pieces.</param>
        /// <param name="controller">Holds and drops the next piece.</param>
        /// <param name="overflow">Detects the game over.</param>
        /// <param name="score">Score of the scene; bound here to the merges and the drops.</param>
        /// <param name="clock">Gives the time of the run timer; <see cref="Time.timeAsDouble"/> when null.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public RunSystems(
            GameConfig config,
            IReadOnlyList<TierDefinition> tiers,
            IAssetService assets,
            PieceFactory factory,
            MergeSystem merge,
            DropController controller,
            OverflowDetector overflow,
            ScoreSystem score,
            Func<double> clock = null)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _merge = merge != null ? merge : throw new ArgumentNullException(nameof(merge));
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _overflow = overflow != null ? overflow : throw new ArgumentNullException(nameof(overflow));
            Score = score ?? throw new ArgumentNullException(nameof(score));

            _timer = new RunTimer(clock ?? (() => Time.timeAsDouble));
            Score.Bind(_merge, _controller);
        }

        /// <summary>The score of the scene, kept across runs.</summary>
        public ScoreSystem Score { get; }

        /// <summary>
        /// Ends a combo that was not continued, on the clock of the merges. Call it from FixedUpdate.
        /// </summary>
        public void Tick()
        {
            Score.Tick();
        }

        /// <inheritdoc />
        public RunContext PrepareRun(int seed)
        {
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;

            // The pieces first: everything below clears state that refers to them.
            _factory.ReleaseAll();
            var queue = new SpawnQueue(_config, seed);
            _merge.ResetForNewRun();
            _overflow.ResetForNewRun();
            Score.ResetForNewRun();
            _controller.ResetForNewRun(queue);
            return new RunContext(Score, queue, _tiers, _assets);
        }

        /// <inheritdoc />
        public void BeginPlaying()
        {
            _timer.Start();
            _merge.enabled = true;
            _controller.Enable();
            _overflow.Enable();
        }

        /// <inheritdoc />
        public RunSummary StopPlaying()
        {
            _timer.Stop();
            _controller.Disable();
            _overflow.Disable();
            _merge.enabled = false;
            Physics2D.simulationMode = SimulationMode2D.Script;

            var summary = RunSummary.From(Score, _timer.ElapsedSeconds);

            // The score system does not keep the best score by itself, so the next run starts from it.
            Score.BestScore = summary.BestScore;
            return summary;
        }

        /// <summary>
        /// Stops listening to the merges and the drops, and gives the physics back.
        /// </summary>
        public void Dispose()
        {
            Score.Unbind();
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }
    }
}
