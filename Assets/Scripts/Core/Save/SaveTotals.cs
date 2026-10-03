using System;

namespace Coika.Core
{
    /// <summary>
    /// Lifetime counters across all runs.
    /// </summary>
    [Serializable]
    public class SaveTotals
    {
        /// <summary>Runs finished.</summary>
        public int games;

        /// <summary>Merges performed.</summary>
        public int merges;

        /// <summary>Seconds played.</summary>
        public float playSeconds;
    }
}
