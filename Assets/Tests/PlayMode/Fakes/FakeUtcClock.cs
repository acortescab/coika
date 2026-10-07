using System;
using Coika.Core;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// A UTC clock the test sets, so no test reads the real clock.
    /// </summary>
    public sealed class FakeUtcClock : IUtcClock
    {
        /// <summary>The date and time the clock reads.</summary>
        public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    }
}
