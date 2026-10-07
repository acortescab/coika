namespace Coika.Gameplay
{
    /// <summary>
    /// What the <see cref="GameManager"/> asks of the gameplay systems. The manager decides when, this decides how,
    /// so the manager holds no gameplay rule and is tested with a fake.
    /// </summary>
    public interface IRunSystems
    {
        /// <summary>
        /// Clears everything of the previous run and builds the new one, without starting it: no piece in play, a
        /// new spawn queue for the seed, score, combo, overflow and merges at their starting values.
        /// </summary>
        /// <param name="seed">Seed of the run's spawn queue.</param>
        /// <param name="rules">Rules of the run's mode, which decide its end condition.</param>
        /// <returns>What the UI needs to show the run.</returns>
        RunContext PrepareRun(int seed, IGameModeRules rules);

        /// <summary>
        /// Lets the prepared run play: starts the timer, enables the drop and the merges, enables the overflow
        /// detector when the mode ends on overflow, and lets the physics run.
        /// </summary>
        void BeginPlaying();

        /// <summary>
        /// Stops the run: stops the timer, disables the drop, the merges and the overflow detector and freezes the
        /// physics. The pieces stay where they are.
        /// </summary>
        /// <returns>The summary of the run.</returns>
        RunSummary StopPlaying();
    }
}
