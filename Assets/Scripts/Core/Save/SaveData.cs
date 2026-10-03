using System;
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// Root of the persisted save file (GDD §13). A plain serializable class with no <see cref="UnityEngine.Object"/>
    /// references (S-33), written as JSON with <see cref="JsonUtility"/>. The schema is additive: new fields keep
    /// their defaults when an older file lacks them, so only a breaking change bumps <see cref="version"/>.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Schema version written by this build.</summary>
        public const int CURRENT_VERSION = 1;

        /// <summary>Number of tiers whose discovery is tracked.</summary>
        public const int TIER_COUNT = 11;

        /// <summary>Schema version of the file.</summary>
        public int version = CURRENT_VERSION;

        /// <summary>Best score per game mode.</summary>
        public BestScores bestScore = new BestScores();

        /// <summary>Highest tier index ever reached.</summary>
        public int highestTier;

        /// <summary>Lifetime counters.</summary>
        public SaveTotals totals = new SaveTotals();

        /// <summary>Whether each tier has been reached; the first tier always counts as discovered.</summary>
        public bool[] discoveredTiers = NewDiscoveredTiers();

        /// <summary>Unlocked achievement ids (filled in M3).</summary>
        public string[] achievements = new string[0];

        /// <summary>User settings.</summary>
        public SettingsData settings = new SettingsData();

        /// <summary>
        /// Repairs fields a hand-edited or older file may have left null, short or out of range.
        /// </summary>
        public void Normalize()
        {
            bestScore ??= new BestScores();
            bestScore.daily ??= new DailyBest();
            totals ??= new SaveTotals();
            achievements ??= new string[0];
            settings ??= new SettingsData();
            settings.Clamp();

            if (discoveredTiers == null || discoveredTiers.Length != TIER_COUNT)
            {
                var fixedTiers = new bool[TIER_COUNT];
                if (discoveredTiers != null)
                {
                    Array.Copy(discoveredTiers, fixedTiers, Math.Min(discoveredTiers.Length, TIER_COUNT));
                }

                discoveredTiers = fixedTiers;
            }

            discoveredTiers[0] = true;
        }

        /// <summary>
        /// Folds a finished run into the persistent progress.
        /// </summary>
        /// <param name="score">Final score of the run.</param>
        /// <param name="highestTierReached">Highest tier index reached in the run.</param>
        /// <param name="merges">Merges performed in the run.</param>
        /// <param name="playSeconds">Play time of the run in seconds.</param>
        public void RecordRun(int score, int highestTierReached, int merges, float playSeconds)
        {
            bestScore.classic = Math.Max(bestScore.classic, score);
            highestTier = Math.Max(highestTier, highestTierReached);
            totals.games++;
            totals.merges += merges;
            totals.playSeconds += Math.Max(0f, playSeconds);

            var top = Math.Min(highestTierReached, TIER_COUNT - 1);
            for (var i = 0; i <= top; i++)
            {
                discoveredTiers[i] = true;
            }
        }

        /// <summary>
        /// Resets scores, totals and discovered tiers, keeping the settings.
        /// </summary>
        public void ResetProgress()
        {
            bestScore = new BestScores();
            highestTier = 0;
            totals = new SaveTotals();
            discoveredTiers = NewDiscoveredTiers();
        }

        /// <summary>
        /// Creates the discovery flags with only the first tier set.
        /// </summary>
        /// <returns>A new flag array.</returns>
        private static bool[] NewDiscoveredTiers()
        {
            var tiers = new bool[TIER_COUNT];
            tiers[0] = true;
            return tiers;
        }
    }
}
