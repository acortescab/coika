using Coika.Data;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Builds <see cref="SpawnSettings"/> for the spawn tests, so they share the GDD values and one seed.
    /// </summary>
    public static class TestSpawnSettings
    {
        /// <summary>Seed used by the tests that need a fixed sequence.</summary>
        public const int SEED = 12345;

        /// <summary>The GDD weights of the five spawnable tiers. Tests must not change them.</summary>
        public static readonly float[] DefaultWeights = { 30f, 28f, 20f, 14f, 8f };

        /// <summary>
        /// The GDD settings: weights 30/28/20/14/8, five tiers, at most three in a row, opening 0, 1, 0.
        /// </summary>
        /// <returns>The settings.</returns>
        public static SpawnSettings Default()
        {
            return new SpawnSettings(DefaultWeights, 5, 3, new[] { 0, 1, 0 });
        }

        /// <summary>
        /// Settings with the given weights, anti-streak maximum and forced opening, for as many tiers as weights.
        /// </summary>
        /// <param name="weights">Relative weight of each tier.</param>
        /// <param name="antiStreakMax">How many times in a row the same tier may appear.</param>
        /// <param name="opening">Tiers of the first pieces. None for no forced opening.</param>
        /// <returns>The settings.</returns>
        public static SpawnSettings For(float[] weights, int antiStreakMax, params int[] opening)
        {
            return new SpawnSettings(weights, weights.Length, antiStreakMax, opening);
        }
    }
}
