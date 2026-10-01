using System;
using System.Collections.Generic;

namespace Coika.Data
{
    /// <summary>
    /// The values that decide which tier is dropped next: the weight of each spawnable tier, how many times in a
    /// row the same tier may appear, and the forced opening. It is immutable and has no Unity types, so the spawn
    /// logic and its tests do not need a ScriptableObject. <see cref="GameConfig.CreateSpawnSettings"/> builds it from
    /// the config, and <see cref="Validate"/> is the single place that decides whether the values are acceptable.
    /// </summary>
    public sealed class SpawnSettings
    {
        /// <summary>Largest number of spawnable tiers: every tier of a theme.</summary>
        public const int MAX_TIER_COUNT = ThemeDefinition.TIER_COUNT;

        private readonly float[] _weights;
        private readonly int[] _forcedOpening;

        /// <summary>
        /// Creates the settings after validating them. The arrays are copied, so changing them afterwards does not
        /// change the settings.
        /// </summary>
        /// <param name="weights">Relative weight of each spawnable tier, from tier 0. They need not sum to 100.</param>
        /// <param name="spawnableTierCount">Number of spawnable tiers, from 1 to <see cref="MAX_TIER_COUNT"/>.</param>
        /// <param name="antiStreakMax">How many times in a row the same tier may appear. At least 1.</param>
        /// <param name="forcedOpening">Tiers of the first pieces of a run, in order. May be empty.</param>
        /// <exception cref="ArgumentException">A value is not acceptable; the message lists every problem.</exception>
        public SpawnSettings(float[] weights, int spawnableTierCount, int antiStreakMax, int[] forcedOpening)
        {
            var errors = Validate(weights, spawnableTierCount, antiStreakMax, forcedOpening);
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join(" ", errors));
            }

            _weights = (float[])weights.Clone();
            _forcedOpening = (int[])forcedOpening.Clone();
            TierCount = spawnableTierCount;
            AntiStreakMax = antiStreakMax;
        }

        /// <summary>Number of spawnable tiers. Only tiers 0 up to this count minus one can be dropped.</summary>
        public int TierCount { get; }

        /// <summary>How many times in a row the same tier may appear.</summary>
        public int AntiStreakMax { get; }

        /// <summary>Number of pieces at the start of a run whose tier is forced.</summary>
        public int ForcedOpeningLength => _forcedOpening.Length;

        /// <summary>
        /// Weight of a tier. Read through an index so the spawn logic can use it without allocating.
        /// </summary>
        /// <param name="tier">A spawnable tier, from 0 to <see cref="TierCount"/> minus one.</param>
        /// <returns>The weight, zero or more.</returns>
        public float GetWeight(int tier)
        {
            return _weights[tier];
        }

        /// <summary>
        /// Tier of one of the forced opening pieces.
        /// </summary>
        /// <param name="index">Position in the opening, from 0 to <see cref="ForcedOpeningLength"/> minus one.</param>
        /// <returns>The forced tier.</returns>
        public int GetForcedTier(int index)
        {
            return _forcedOpening[index];
        }

        /// <summary>
        /// Checks the values and describes every problem found. It never throws and never loops, so a bad config
        /// gives a clear message instead of a failure later in the game.
        /// </summary>
        /// <param name="weights">Relative weight of each spawnable tier.</param>
        /// <param name="spawnableTierCount">Number of spawnable tiers.</param>
        /// <param name="antiStreakMax">How many times in a row the same tier may appear.</param>
        /// <param name="forcedOpening">Tiers of the first pieces of a run.</param>
        /// <returns>One message per problem; empty when the values are valid.</returns>
        public static List<string> Validate(float[] weights, int spawnableTierCount, int antiStreakMax, int[] forcedOpening)
        {
            var errors = new List<string>();

            if (spawnableTierCount < 1 || spawnableTierCount > MAX_TIER_COUNT)
            {
                errors.Add($"The spawnable tier count must be between 1 and {MAX_TIER_COUNT}, got {spawnableTierCount}.");
            }

            if (antiStreakMax < 1)
            {
                errors.Add($"The anti-streak maximum must be at least 1, got {antiStreakMax}.");
            }

            ValidateWeights(weights, spawnableTierCount, errors);
            ValidateForcedOpening(forcedOpening, spawnableTierCount, antiStreakMax, errors);
            return errors;
        }

        /// <summary>
        /// Adds an error for every problem of the weights: a missing array, a length that does not match the tier
        /// count, a weight that is negative or not a finite number, or no weight above zero.
        /// </summary>
        /// <param name="weights">Relative weight of each spawnable tier.</param>
        /// <param name="spawnableTierCount">Number of spawnable tiers.</param>
        /// <param name="errors">List the errors are added to.</param>
        private static void ValidateWeights(float[] weights, int spawnableTierCount, List<string> errors)
        {
            if (weights == null)
            {
                errors.Add("The spawn weights are missing.");
                return;
            }

            if (weights.Length != spawnableTierCount)
            {
                errors.Add($"There are {weights.Length} spawn weights but {spawnableTierCount} spawnable tiers; they must match.");
            }

            var hasPositiveWeight = false;
            for (int i = 0; i < weights.Length; i++)
            {
                var weight = weights[i];
                if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f)
                {
                    errors.Add($"The weight of tier {i} must be zero or more and a finite number, got {weight}.");
                }
                else if (weight > 0f)
                {
                    hasPositiveWeight = true;
                }
            }

            if (!hasPositiveWeight)
            {
                errors.Add("At least one spawn weight must be above zero.");
            }
        }

        /// <summary>
        /// Adds an error for every problem of the forced opening: a missing array, a tier outside the spawnable
        /// range, or a tier repeated more times in a row than the anti-streak maximum, because the opening is never
        /// re-rolled and would break the rule on its own.
        /// </summary>
        /// <param name="forcedOpening">Tiers of the first pieces of a run.</param>
        /// <param name="spawnableTierCount">Number of spawnable tiers.</param>
        /// <param name="antiStreakMax">How many times in a row the same tier may appear.</param>
        /// <param name="errors">List the errors are added to.</param>
        private static void ValidateForcedOpening(int[] forcedOpening, int spawnableTierCount, int antiStreakMax, List<string> errors)
        {
            if (forcedOpening == null)
            {
                errors.Add("The forced opening is missing; use an empty array for none.");
                return;
            }

            var run = 0;
            for (int i = 0; i < forcedOpening.Length; i++)
            {
                var tier = forcedOpening[i];
                if (tier < 0 || tier >= spawnableTierCount)
                {
                    errors.Add($"The forced opening piece {i} is tier {tier}, outside the spawnable range 0 to {spawnableTierCount - 1}.");
                }

                run = i > 0 && tier == forcedOpening[i - 1] ? run + 1 : 1;
                if (run == antiStreakMax + 1)
                {
                    errors.Add($"The forced opening repeats tier {tier} more than {antiStreakMax} times in a row, which the anti-streak rule does not allow.");
                }
            }
        }
    }
}
