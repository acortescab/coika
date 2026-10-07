using Coika.Core;

namespace Coika.Gameplay
{
    /// <summary>
    /// What makes a game mode different from the others, as data-like members: the seed, the end condition and where
    /// the best score is stored. No Unity types, so the rules are tested in EditMode. A new mode is a new
    /// implementation registered in <see cref="GameModeRules"/>; no consumer branches on the mode.
    /// </summary>
    public interface IGameModeRules
    {
        /// <summary>Whether a settled piece above the Danger Line ends the run.</summary>
        bool EndsOnOverflow { get; }

        /// <summary>Whether the Danger Line is shown.</summary>
        bool ShowsDangerLine { get; }

        /// <summary>Whether the best score of the mode is recorded.</summary>
        bool RecordsBestScore { get; }

        /// <summary>Whether a retry draws a new seed; when false a retry replays the seed of the first run.</summary>
        bool IsFreshPerRun { get; }

        /// <summary>Key of the mode's best score in the save, matching its field name.</summary>
        string SaveKey { get; }

        /// <summary>
        /// Gives the seed of a new run. This is the only place the clock is read, once at run start (S-63).
        /// </summary>
        /// <param name="seeds">Source of a fresh seed.</param>
        /// <param name="clock">UTC clock, for modes whose seed is the date.</param>
        /// <returns>The seed.</returns>
        int ResolveSeed(ISeedSource seeds, IUtcClock clock);
    }
}
