using System;
using System.Collections.Generic;
using Coika.Core;
using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the rules table of the game modes (issue #66): the documented flags, that a new mode is only a new
    /// entry, and that an unknown mode never falls back to Classic.
    /// </summary>
    public class GameModeRulesTests
    {
        private const GameMode FOURTH_MODE = (GameMode)3;

        /// <summary>
        /// Each mode returns the flags the GDD documents for it.
        /// </summary>
        [TestCase(GameMode.Classic, true, true, true, true, "classic")]
        [TestCase(GameMode.Daily, true, true, true, false, "daily")]
        [TestCase(GameMode.Zen, false, false, false, true, "zen")]
        public void Get_ForEachMode_ReturnsTheDocumentedFlags(
            GameMode mode, bool endsOnOverflow, bool showsDangerLine, bool recordsBest, bool freshPerRun, string saveKey)
        {
            var rules = GameModeRules.CreateDefault().Get(mode);

            Assert.AreEqual(endsOnOverflow, rules.EndsOnOverflow);
            Assert.AreEqual(showsDangerLine, rules.ShowsDangerLine);
            Assert.AreEqual(recordsBest, rules.RecordsBestScore);
            Assert.AreEqual(freshPerRun, rules.IsFreshPerRun);
            Assert.AreEqual(saveKey, rules.SaveKey);
        }

        /// <summary>
        /// The save key of every default mode is a field of the best scores, so the key and the save agree.
        /// </summary>
        [Test]
        public void SaveKey_OfEveryMode_IsAFieldOfBestScores()
        {
            var table = GameModeRules.CreateDefault();

            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                Assert.IsNotNull(typeof(BestScores).GetField(table.Get(mode).SaveKey), $"{mode} has no best score field.");
            }
        }

        /// <summary>
        /// A fourth mode is one entry in the table: it resolves its rules and seed like the others, and the
        /// others are untouched.
        /// </summary>
        [Test]
        public void Get_WithAFourthFakeMode_ResolvesItWithoutChangingTheOthers()
        {
            var table = new GameModeRules(new Dictionary<GameMode, IGameModeRules>
            {
                { GameMode.Classic, new ClassicRules() },
                { FOURTH_MODE, new FakeRules() }
            });

            var rules = table.Get(FOURTH_MODE);

            Assert.IsInstanceOf<FakeRules>(rules);
            Assert.AreEqual(77, rules.ResolveSeed(new FakeSeedSource { Seed = 5 }, new FakeUtcClock()));
            Assert.IsInstanceOf<ClassicRules>(table.Get(GameMode.Classic));
        }

        /// <summary>
        /// A mode with no entry is an explicit error, never Classic.
        /// </summary>
        [Test]
        public void Get_ForAnUnregisteredMode_Throws()
        {
            var table = new GameModeRules(new Dictionary<GameMode, IGameModeRules> { { GameMode.Classic, new ClassicRules() } });

            Assert.Throws<InvalidOperationException>(() => table.Get(GameMode.Zen));
        }

        /// <summary>
        /// The table needs its entries.
        /// </summary>
        [Test]
        public void New_WithNullTableOrRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GameModeRules(null));
            Assert.Throws<ArgumentNullException>(() => new GameModeRules(new Dictionary<GameMode, IGameModeRules> { { GameMode.Classic, null } }));
        }

        /// <summary>
        /// Rules of an invented mode, to prove the table needs no production change.
        /// </summary>
        private sealed class FakeRules : IGameModeRules
        {
            /// <inheritdoc />
            public bool EndsOnOverflow => false;

            /// <inheritdoc />
            public bool ShowsDangerLine => true;

            /// <inheritdoc />
            public bool RecordsBestScore => false;

            /// <inheritdoc />
            public bool IsFreshPerRun => true;

            /// <inheritdoc />
            public string SaveKey => "fake";

            /// <inheritdoc />
            public int ResolveSeed(ISeedSource seeds, IUtcClock clock)
            {
                return 77;
            }
        }
    }
}
