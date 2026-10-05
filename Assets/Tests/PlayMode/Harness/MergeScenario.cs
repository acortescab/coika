using System;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Scripted merges for the tests that need one on demand, without waiting for a random drop sequence.
    /// </summary>
    public static class MergeScenario
    {
        private const int MAX_STEPS = 600;
        private const float HEIGHT_ABOVE_FLOOR = 6f;
        private const float OVERLAP = 0.45f;

        /// <summary>
        /// Gives the height where <see cref="MergeTwo"/> places the pair.
        /// </summary>
        /// <param name="world">The world with the jar.</param>
        /// <returns>The world Y of the pair, above the jar floor.</returns>
        public static float PairHeight(SimulationWorld world)
        {
            return world.Jar.FloorY + HEIGHT_ABOVE_FLOOR;
        }

        /// <summary>
        /// Creates two overlapping pieces of a tier, side by side around x = 0, and steps until they merge.
        /// </summary>
        /// <param name="world">A started run.</param>
        /// <param name="tier">Tier of the two pieces.</param>
        /// <param name="alsoDone">An extra stop condition, such as a supernova that is not counted as a merge.</param>
        public static void MergeTwo(SimulationWorld world, int tier, Func<bool> alsoDone = null)
        {
            var radius = world.Tiers[tier].Radius;
            var y = PairHeight(world);
            var before = world.Score.Merges;
            world.Factory.Create(world.Tiers[tier], new Vector2(-radius * OVERLAP, y), Vector2.zero);
            world.Factory.Create(world.Tiers[tier], new Vector2(radius * OVERLAP, y), Vector2.zero);

            bool Done() => world.Score.Merges > before || (alsoDone != null && alsoDone());
            for (var i = 0; i < MAX_STEPS && !Done(); i++)
            {
                world.Step();
            }

            if (!Done())
            {
                throw new InvalidOperationException($"The two pieces of tier {tier} did not merge in {MAX_STEPS} steps.");
            }
        }
    }
}
