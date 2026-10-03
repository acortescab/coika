namespace Coika.Gameplay
{
    /// <summary>
    /// The states of the game (GDD §3). M1 only uses <see cref="Playing"/> and <see cref="GameOver"/>; the others
    /// exist, with their transitions, so the menu, the pause and the boot flow of M2 and M3 need no redesign.
    /// </summary>
    public enum GameState
    {
        /// <summary>The game is starting and the assets are loading.</summary>
        Boot,

        /// <summary>The main menu is open.</summary>
        Menu,

        /// <summary>A run is being played.</summary>
        Playing,

        /// <summary>A run is paused.</summary>
        Paused,

        /// <summary>The run ended and the Game Over view is the only thing that reacts.</summary>
        GameOver,
    }
}
