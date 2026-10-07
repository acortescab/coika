namespace Coika.Core
{
    /// <summary>
    /// How a run is played (GDD §12). The modes differ only in their seed, their end condition and where the best
    /// score is stored; those differences live in the rules of each mode, not in branches on this value.
    /// </summary>
    public enum GameMode
    {
        /// <summary>Endless run with a fresh seed.</summary>
        Classic = 0,

        /// <summary>One seed per UTC day.</summary>
        Daily = 1,

        /// <summary>No game over and no Danger Line.</summary>
        Zen = 2
    }
}
