using System;

namespace Coika.Core
{
    /// <summary>
    /// The real <see cref="ISeedSource"/>: a seed from the system tick count, as Classic runs used before modes existed.
    /// </summary>
    public sealed class SystemSeedSource : ISeedSource
    {
        /// <inheritdoc />
        public int NextSeed()
        {
            return Environment.TickCount;
        }
    }
}
