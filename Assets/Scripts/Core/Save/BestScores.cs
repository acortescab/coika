using System;

namespace Coika.Core
{
    /// <summary>
    /// Best score per game mode. The field names are the <c>SaveKey</c> of each mode; read and write them through
    /// <see cref="SaveData.GetBest"/> and <see cref="SaveData.SetBest"/>.
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
