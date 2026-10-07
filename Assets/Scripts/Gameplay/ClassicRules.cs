using Coika.Core;

namespace Coika.Gameplay
{
    /// <summary>
    /// Rules of <see cref="GameMode.Classic"/>: endless, ends on overflow, a fresh seed every run.
    /// </summary>
    public sealed class ClassicRules : IGameModeRules
    {
        /// <inheritdoc />
        public bool EndsOnOverflow => true;

        /// <inheritdoc />
        public bool ShowsDangerLine => true;

        /// <inheritdoc />
        public bool RecordsBestScore => true;

        /// <inheritdoc />
        public bool IsFreshPerRun => true;

        /// <inheritdoc />
        public string SaveKey => "classic";

        /// <inheritdoc />
        public int ResolveSeed(ISeedSource seeds, IUtcClock clock)
        {
            return seeds.NextSeed();
        }
    }
}
