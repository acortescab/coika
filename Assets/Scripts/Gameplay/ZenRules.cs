using Coika.Core;

namespace Coika.Gameplay
{
    /// <summary>
    /// Rules of <see cref="GameMode.Zen"/>: no game over, no Danger Line, no best score, a fresh seed every run.
    /// </summary>
    public sealed class ZenRules : IGameModeRules
    {
        /// <inheritdoc />
        public bool EndsOnOverflow => false;

        /// <inheritdoc />
        public bool ShowsDangerLine => false;

        /// <inheritdoc />
        public bool RecordsBestScore => false;

        /// <inheritdoc />
        public bool IsFreshPerRun => true;

        /// <inheritdoc />
        public string SaveKey => "zen";

        /// <inheritdoc />
        public int ResolveSeed(ISeedSource seeds, IUtcClock clock)
        {
            return seeds.NextSeed();
        }
    }
}
