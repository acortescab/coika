using Coika.Core;

namespace Coika.Gameplay
{
    /// <summary>
    /// Rules of <see cref="GameMode.Daily"/>: like Classic, but the seed is the UTC date as yyyyMMdd and a retry
    /// replays the same board.
    /// </summary>
    public sealed class DailyRules : IGameModeRules
    {
        /// <inheritdoc />
        public bool EndsOnOverflow => true;

        /// <inheritdoc />
        public bool ShowsDangerLine => true;

        /// <inheritdoc />
        public bool RecordsBestScore => true;

        /// <inheritdoc />
        public bool IsFreshPerRun => false;

        /// <inheritdoc />
        public string SaveKey => "daily";

        /// <inheritdoc />
        public int ResolveSeed(ISeedSource seeds, IUtcClock clock)
        {
            var now = clock.UtcNow;
            return DayKey(now.Year, now.Month, now.Day);
        }

        /// <summary>
        /// Packs a date as the integer yyyyMMdd, without allocating.
        /// </summary>
        /// <param name="year">Year.</param>
        /// <param name="month">Month, 1 to 12.</param>
        /// <param name="day">Day of the month.</param>
        /// <returns>The date as yyyyMMdd.</returns>
        public static int DayKey(int year, int month, int day)
        {
            return year * 10000 + month * 100 + day;
        }
    }
}
