using Coika.Data;

namespace Coika.Gameplay
{
    /// <summary>
    /// The weighted choice of a tier, as a pure function: given the weights, a tier to leave out and a number
    /// between 0 and 1, it returns a tier. It holds no state and allocates nothing, so the spawn queue can call it
    /// every piece and the maths can be tested with exact rolls instead of statistics.
    /// </summary>
    public static class SpawnSelector
    {
        /// <summary>Value for "no tier is left out".</summary>
        public const int NO_TIER = -1;

        /// <summary>
        /// Picks a tier with a probability proportional to its weight, leaving out one tier. The weights of the
        /// other tiers are used as they are, which is the same as renormalizing them. A tier whose weight is zero is
        /// never picked. If leaving the tier out would leave no weight at all (a single spawnable tier, or only the
        /// excluded tier has weight), the exclusion is ignored so the choice never fails: the tier repeats.
        /// </summary>
        /// <param name="settings">The weights of the spawnable tiers.</param>
        /// <param name="excludedTier">Tier to leave out, or <see cref="NO_TIER"/>.</param>
        /// <param name="roll">A number from 0 up to, but not including, 1.</param>
        /// <returns>The picked tier.</returns>
        public static int Pick(SpawnSettings settings, int excludedTier, double roll)
        {
            var total = SumWeights(settings, excludedTier);
            if (total <= 0.0)
            {
                excludedTier = NO_TIER;
                total = SumWeights(settings, NO_TIER);
            }

            var target = roll * total;
            var cumulative = 0.0;
            var lastTier = NO_TIER;

            for (int tier = 0; tier < settings.TierCount; tier++)
            {
                var weight = settings.GetWeight(tier);
                if (tier == excludedTier || weight <= 0f)
                {
                    continue;
                }

                cumulative += weight;
                lastTier = tier;
                if (target < cumulative)
                {
                    return tier;
                }
            }

            // Floating-point rounding can leave the target at the very top of the range: take the last tier.
            return lastTier;
        }

        /// <summary>
        /// Adds up the weights of every tier except the excluded one.
        /// </summary>
        /// <param name="settings">The weights of the spawnable tiers.</param>
        /// <param name="excludedTier">Tier to leave out, or <see cref="NO_TIER"/>.</param>
        /// <returns>The sum of the weights.</returns>
        private static double SumWeights(SpawnSettings settings, int excludedTier)
        {
            var total = 0.0;
            for (int tier = 0; tier < settings.TierCount; tier++)
            {
                if (tier != excludedTier)
                {
                    total += settings.GetWeight(tier);
                }
            }

            return total;
        }
    }
}
