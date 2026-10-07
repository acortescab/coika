using Coika.Core;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// A seed source the test controls: it gives <see cref="Seed"/> and moves it on by one after each call when
    /// <see cref="Increments"/> is set.
    /// </summary>
    public sealed class FakeSeedSource : ISeedSource
    {
        /// <summary>The seed given to the next run.</summary>
        public int Seed { get; set; }

        /// <summary>Whether the seed grows by one after each call.</summary>
        public bool Increments { get; set; }

        /// <inheritdoc />
        public int NextSeed()
        {
            return Increments ? Seed++ : Seed;
        }
    }
}
