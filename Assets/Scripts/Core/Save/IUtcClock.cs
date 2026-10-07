using System;

namespace Coika.Core
{
    /// <summary>
    /// The wall clock in UTC, for what depends on the calendar day (the Daily seed and best score). A seam, so the
    /// logic never reads the real clock and tests set the date.
    /// </summary>
    public interface IUtcClock
    {
        /// <summary>Current date and time in UTC.</summary>
        DateTime UtcNow { get; }
    }
}
