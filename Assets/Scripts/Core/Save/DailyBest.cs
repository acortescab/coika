using System;

namespace Coika.Core
{
    /// <summary>
    /// Best score of a Daily challenge day.
    /// </summary>
    [Serializable]
    public class DailyBest
    {
        /// <summary>Day of the score as yyyyMMdd, empty when none was set.</summary>
        public string date = string.Empty;

        /// <summary>Best score of that day.</summary>
        public int score;
    }
}
