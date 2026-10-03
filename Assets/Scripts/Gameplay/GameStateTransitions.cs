namespace Coika.Gameplay
{
    /// <summary>
    /// The table of legal state changes, in one place so the manager and the tests agree. Starting a run is legal
    /// from every state, including <see cref="GameState.Playing"/>, where it restarts the run.
    /// </summary>
    public static class GameStateTransitions
    {
        /// <summary>
        /// Whether the game may go from one state to another.
        /// </summary>
        /// <param name="from">Current state.</param>
        /// <param name="to">Wanted state.</param>
        /// <returns>True when the change is allowed.</returns>
        public static bool IsAllowed(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.Playing:
                    return true;
                case GameState.Menu:
                    return from == GameState.Boot || from == GameState.GameOver || from == GameState.Paused;
                case GameState.Paused:
                    return from == GameState.Playing;
                case GameState.GameOver:
                    return from == GameState.Playing || from == GameState.Paused;
                default:
                    return false;
            }
        }
    }
}
