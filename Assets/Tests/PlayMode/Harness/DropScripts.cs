using System;
using System.Collections.Generic;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Builders of the drop X lists the simulation tests play. They use <see cref="System.Random"/> with a given seed,
    /// never <c>UnityEngine.Random</c>, so a script is the same on every machine and in every run (S-63).
    /// </summary>
    public static class DropScripts
    {
        /// <summary>Half of the width inside which the random drops fall: the jar is 10 wide, the controller clamps the rest.</summary>
        public const float RANDOM_HALF_RANGE = 4.5f;

        /// <summary>
        /// Builds a list of random drop positions across the whole jar.
        /// </summary>
        /// <param name="seed">Seed of the generator. The same seed gives the same list.</param>
        /// <param name="count">Number of drops.</param>
        /// <returns>The X of each drop, between -4.5 and 4.5.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
        public static IReadOnlyList<float> Random(int seed, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var random = new System.Random(seed);
            var drops = new float[count];
            for (var i = 0; i < count; i++)
            {
                drops[i] = (float)((random.NextDouble() * 2.0 - 1.0) * RANDOM_HALF_RANGE);
            }

            return drops;
        }

        /// <summary>
        /// Builds a list that drops every piece at the same X, so the pieces stack in one column.
        /// </summary>
        /// <param name="x">World X of every drop.</param>
        /// <param name="count">Number of drops.</param>
        /// <returns>The X of each drop.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
        public static IReadOnlyList<float> Column(float x, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var drops = new float[count];
            for (var i = 0; i < count; i++)
            {
                drops[i] = x;
            }

            return drops;
        }
    }
}
