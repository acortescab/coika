using System;
using System.Collections.Generic;
using Coika.Core;

namespace Coika.Gameplay
{
    /// <summary>
    /// The table that maps each <see cref="GameMode"/> to its <see cref="IGameModeRules"/>. Adding a mode is adding
    /// an entry here; nothing else switches on the mode. It is an ordinary object created by the installer, not a singleton.
    /// </summary>
    public sealed class GameModeRules
    {
        private readonly IReadOnlyDictionary<GameMode, IGameModeRules> _table;

        /// <summary>
        /// Creates the table from its entries.
        /// </summary>
        /// <param name="table">Rules of each mode.</param>
        /// <exception cref="ArgumentNullException">The table or one of its rules is null.</exception>
        public GameModeRules(IReadOnlyDictionary<GameMode, IGameModeRules> table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            foreach (var entry in table)
            {
                if (entry.Value == null)
                {
                    throw new ArgumentNullException(nameof(table), $"The rules of {entry.Key} are null.");
                }
            }
        }

        /// <summary>
        /// Creates the table of the game: Classic, Daily and Zen.
        /// </summary>
        /// <returns>A new table.</returns>
        public static GameModeRules CreateDefault()
        {
            return new GameModeRules(new Dictionary<GameMode, IGameModeRules>
            {
                { GameMode.Classic, new ClassicRules() },
                { GameMode.Daily, new DailyRules() },
                { GameMode.Zen, new ZenRules() }
            });
        }

        /// <summary>
        /// Gives the rules of a mode.
        /// </summary>
        /// <param name="mode">The mode.</param>
        /// <returns>Its rules.</returns>
        /// <exception cref="InvalidOperationException">The mode has no entry; it never falls back to Classic.</exception>
        public IGameModeRules Get(GameMode mode)
        {
            if (!_table.TryGetValue(mode, out var rules))
            {
                throw new InvalidOperationException($"No rules are registered for the mode {mode}.");
            }

            return rules;
        }
    }
}
