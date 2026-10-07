using Coika.Core;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// A seed source the test controls: it gives <see cref="Seed"/> until the test changes it.
    /// </summary>
    public sealed class FakeSeedSource : ISeedSource
    {
        /// <summary>The seed given to the next run.</summary>
        public int Seed { get; set; }

        /// <inheritdoc />
        public int NextSeed()
        {
            return Seed;
        }
    }
}
