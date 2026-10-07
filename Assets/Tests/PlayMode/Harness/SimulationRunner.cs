using System;
using System.Collections.Generic;
using Coika.Gameplay;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Plays a scripted list of drop X positions into a <see cref="SimulationWorld"/> and returns the
    /// <see cref="SimulationResult"/>. It drives the systems through their public API instead of simulating pointer
    /// input (that is covered by the drop controller tests): it points the scripted input at the X, steps the physics
    /// until the held piece is there and the cooldown is over, then raises a click. The whole loop is synchronous, so
    /// it never waits for a frame and does not depend on the frame rate.
    /// </summary>
    public sealed class SimulationRunner
    {
        private const int MAX_STEPS_PER_DROP = 3000;
        private const float REACH_TOLERANCE = 0.001f;

        private readonly SimulationWorld _world;
        private readonly SimulationOptions _options;
        private Action<SimulationWorld> _onStep;
        private bool _stopped;

        /// <summary>
        /// Creates a runner for a world.
        /// </summary>
        /// <param name="world">The world to play in. The runner does not own it.</param>
        /// <param name="options">The options the world was built with.</param>
        /// <exception cref="ArgumentNullException">The world or the options are null.</exception>
        public SimulationRunner(SimulationWorld world, SimulationOptions options)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Builds a fresh world, plays the drops and disposes the world.
        /// </summary>
        /// <param name="options">Seed and rules of the simulation.</param>
        /// <param name="dropXs">World X of each drop, in order.</param>
        /// <param name="onStep">Called after every physics step, for tests that check an invariant at each one. Null for none.</param>
        /// <returns>What the run ended with.</returns>
        public static SimulationResult Run(SimulationOptions options, IReadOnlyList<float> dropXs, Action<SimulationWorld> onStep = null)
        {
            using (var world = new SimulationWorld(options))
            {
                return new SimulationRunner(world, options).Play(dropXs, onStep);
            }
        }

        /// <summary>
        /// Starts the run and plays the drops, then keeps simulating for the settle time of the options, or until the
        /// game is over.
        /// </summary>
        /// <param name="dropXs">World X of each drop, in order. A drop X outside the jar is clamped by the controller.</param>
        /// <param name="onStep">Called after every physics step. Null for none.</param>
        /// <returns>What the run ended with.</returns>
        /// <exception cref="ArgumentNullException">The drops are null.</exception>
        /// <exception cref="InvalidOperationException">A drop could not be made within the step limit, which means the controller is stuck.</exception>
        public SimulationResult Play(IReadOnlyList<float> dropXs, Action<SimulationWorld> onStep = null)
        {
            _world.StartRun();
            return Continue(dropXs, onStep);
        }

        /// <summary>
        /// Plays more drops in the run that is going on, for example the next run after <see cref="SimulationWorld.Restart"/>,
        /// then keeps simulating for the settle time of the options, or until the game is over.
        /// </summary>
        /// <param name="dropXs">World X of each drop, in order.</param>
        /// <param name="onStep">Called after every physics step. Null for none.</param>
        /// <returns>What the world looks like afterwards.</returns>
        /// <exception cref="ArgumentNullException">The drops are null.</exception>
        /// <exception cref="InvalidOperationException">A drop could not be made within the step limit.</exception>
        public SimulationResult Continue(IReadOnlyList<float> dropXs, Action<SimulationWorld> onStep = null)
        {
            Advance(dropXs, onStep);
            return _world.Snapshot();
        }

        /// <summary>
        /// Plays the drops like <see cref="Continue"/> but returns nothing, because taking the snapshot allocates in
        /// proportion to the pieces: a test that counts allocations measures this method.
        /// </summary>
        /// <param name="dropXs">World X of each drop, in order.</param>
        /// <param name="onStep">Called after every physics step. Null for none.</param>
        /// <exception cref="ArgumentNullException">The drops are null.</exception>
        /// <exception cref="InvalidOperationException">A drop could not be made within the step limit.</exception>
        public void Advance(IReadOnlyList<float> dropXs, Action<SimulationWorld> onStep = null)
        {
            if (dropXs == null)
            {
                throw new ArgumentNullException(nameof(dropXs));
            }

            _onStep = onStep;
            _stopped = false;

            for (var i = 0; i < dropXs.Count && !_stopped; i++)
            {
                Drop(dropXs[i]);
                if (!_stopped && PausesAfter(i))
                {
                    PauseForSteps();
                }
            }

            var settleSteps = Mathf.CeilToInt(_options.SettleSeconds / _world.FixedDeltaTime);
            for (var i = 0; i < settleSteps && !_stopped; i++)
            {
                Step();
            }
        }

        /// <summary>
        /// Whether the options ask for a pause after the drop at the index.
        /// </summary>
        /// <param name="dropIndex">Index of the drop in the list being played.</param>
        /// <returns>True when the run is to be paused.</returns>
        private bool PausesAfter(int dropIndex)
        {
            var pauses = _options.PauseAfterDrops;
            if (pauses == null)
            {
                return false;
            }

            for (var i = 0; i < pauses.Count; i++)
            {
                if (pauses[i] == dropIndex)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Pauses the run, attempts the steps of the options (nothing advances, and the step callback is not called) and
        /// resumes it.
        /// </summary>
        private void PauseForSteps()
        {
            _world.Pause();
            for (var i = 0; i < _options.PauseSteps; i++)
            {
                _world.Step();
            }

            _world.Resume();
        }

        /// <summary>
        /// Moves the held piece to the X, waits until a drop is allowed and drops it.
        /// </summary>
        /// <param name="x">World X of the drop.</param>
        /// <exception cref="InvalidOperationException">The drop was not possible within the step limit, or the controller did not accept it.</exception>
        private void Drop(float x)
        {
            _world.Input.HasPointer = true;
            _world.Input.PointerWorldX = x;

            var guard = 0;
            while (!_stopped && !CanDrop(x))
            {
                Step();
                if (++guard > MAX_STEPS_PER_DROP)
                {
                    throw new InvalidOperationException($"The drop at X {x} was not possible after {MAX_STEPS_PER_DROP} steps (state {_world.Controller.State}).");
                }
            }

            if (_stopped)
            {
                return;
            }

            var before = _world.PiecesDropped;
            var restartsBefore = _world.Restarts;
            _world.Input.Click();
            Step();

            if (_world.PiecesDropped == before && !_stopped)
            {
                // A game over in the same step disables the controller before it reads the click: drop again in the new run.
                if (_world.Restarts != restartsBefore)
                {
                    Drop(x);
                    return;
                }

                throw new InvalidOperationException($"The controller did not accept the drop at X {x}.");
            }
        }

        /// <summary>
        /// Whether a click now would drop the held piece at the X: the controller is aiming, the held piece has
        /// reached the X (or the wall that clamps it) and the minimum time since the last drop has passed.
        /// </summary>
        /// <param name="x">World X wanted.</param>
        /// <returns>True when the drop can be made.</returns>
        private bool CanDrop(float x)
        {
            var controller = _world.Controller;
            if (controller.State != DropState.Aiming || controller.HeldPiece == null)
            {
                return false;
            }

            if (_world.SecondsSinceLastDrop < _options.MinSecondsBetweenDrops)
            {
                return false;
            }

            var radius = controller.HeldPiece.Tier.DiameterUnits * 0.5f;
            var target = DropFlow.ClampX(x, radius, _world.Jar.InteriorMin.x, _world.Jar.InteriorMax.x);
            return Mathf.Abs(controller.HeldX - target) <= REACH_TOLERANCE;
        }

        /// <summary>
        /// One physics step, then the step callback, then the handling of a game over: the simulation stops, or the
        /// next run starts, as the options say.
        /// </summary>
        private void Step()
        {
            _world.Step();
            _onStep?.Invoke(_world);

            if (!_world.IsGameOver)
            {
                return;
            }

            if (_options.RestartOnGameOver)
            {
                _world.Restart();
            }
            else
            {
                _stopped = true;
            }
        }
    }
}
