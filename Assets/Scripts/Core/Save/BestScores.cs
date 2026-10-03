using System;

namespace Coika.Core
{
    /// <summary>
    /// Best score per game mode. Only Classic is written until M3 adds Daily and Zen.
    /// </summary>
    [Serializable]
    public class BestScores
    {
        /// <summary>Best Classic score.</summary>
        public int classic;

        /// <summary>Best Daily score and the day it was set.</summary>
        public DailyBest daily = new DailyBest();

        /// <summary>Best Zen score.</summary>
        public int zen;
    }
}
