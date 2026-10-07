namespace Coika.Core
{
    /// <summary>
    /// Gives the seed of a run whose seed is not fixed by the mode. The real one may use the clock; tests inject a
    /// fixed one (S-63: the clock is never read inside the gameplay logic).
    /// </summary>
    public interface ISeedSource
    {
        /// <summary>
        /// Gives a seed for a new run.
        /// </summary>
        /// <returns>The seed.</returns>
        int NextSeed();
    }
}
