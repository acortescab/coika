using System;

namespace Coika.Core
{
    /// <summary>
    /// The real <see cref="IUtcClock"/>, backed by <see cref="DateTime.UtcNow"/>.
    /// </summary>
    public sealed class SystemUtcClock : IUtcClock
    {
        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
