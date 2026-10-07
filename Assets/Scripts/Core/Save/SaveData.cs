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
        /// <param name="mode">Mode the run was played in.</param>
        /// <param name="recordsBest">Whether the mode records its best score; progress is folded in either way.</param>
        /// <param name="dayKey">UTC day as yyyyMMdd, used by the Daily best.</param>
        public void RecordRun(
            int score,
            int highestTierReached,
            int merges,
            float playSeconds,
            GameMode mode = GameMode.Classic,
            bool recordsBest = true,
            int dayKey = 0)
        {
            if (recordsBest)
            {
                SetBest(mode, score, dayKey);
            }

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
        /// Reads the best score of a mode.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="dayKey">Today in UTC as yyyyMMdd; a Daily best set on another day reads as 0.</param>
        /// <returns>The best score, 0 when none.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The mode has no best score field.</exception>
        public int GetBest(GameMode mode, int dayKey = 0)
        {
            switch (mode)
            {
                case GameMode.Classic:
                    return bestScore.classic;
                case GameMode.Zen:
                    return bestScore.zen;
                case GameMode.Daily:
                    return int.TryParse(bestScore.daily.date, out var day) && day == dayKey ? bestScore.daily.score : 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "The mode has no best score field.");
            }
        }

        /// <summary>
        /// Raises the best score of a mode; a lower score is ignored. A Daily score of a new day replaces the old one.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <param name="score">Score of the run.</param>
        /// <param name="dayKey">Today in UTC as yyyyMMdd, stored with the Daily best.</param>
        /// <exception cref="ArgumentOutOfRangeException">The mode has no best score field.</exception>
        public void SetBest(GameMode mode, int score, int dayKey = 0)
        {
            switch (mode)
            {
                case GameMode.Classic:
                    bestScore.classic = Math.Max(bestScore.classic, score);
                    break;
                case GameMode.Zen:
                    bestScore.zen = Math.Max(bestScore.zen, score);
                    break;
                case GameMode.Daily:
                    var best = Math.Max(GetBest(GameMode.Daily, dayKey), score);
                    bestScore.daily.date = dayKey.ToString();
                    bestScore.daily.score = best;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "The mode has no best score field.");
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
