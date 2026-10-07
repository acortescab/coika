using System;
using Coika.Core;
using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks how each mode resolves the seed of a run (issue #66): the UTC date for Daily, the seed source for
    /// Classic and Zen. A fake clock and a fake seed source stand in for the real ones.
    /// </summary>
    public class DailySeedTests
    {
        private readonly FakeUtcClock _clock = new FakeUtcClock();
        private readonly FakeSeedSource _seeds = new FakeSeedSource { Seed = 1234 };

        /// <summary>
        /// A clock at 2026-10-07 UTC gives the seed 20261007.
        /// </summary>
        [Test]
        public void ResolveSeed_DailyOnTheSeventhOfOctober_IsTheDate()
        {
            _clock.UtcNow = new DateTime(2026, 10, 7, 9, 30, 0, DateTimeKind.Utc);

            Assert.AreEqual(20261007, new DailyRules().ResolveSeed(_seeds, _clock));
        }

        /// <summary>
        /// The seed changes at UTC midnight.
        /// </summary>
        [Test]
        public void ResolveSeed_DailyAcrossMidnight_GivesTwoSeeds()
        {
            _clock.UtcNow = new DateTime(2026, 10, 7, 23, 59, 59, DateTimeKind.Utc);
            var before = new DailyRules().ResolveSeed(_seeds, _clock);
            _clock.UtcNow = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
            var after = new DailyRules().ResolveSeed(_seeds, _clock);

            Assert.AreEqual(20261007, before);
            Assert.AreEqual(20261008, after);
        }

        /// <summary>
        /// Daily ignores the seed source: the date is the whole seed.
        /// </summary>
        [Test]
        public void ResolveSeed_Daily_IgnoresTheSeedSource()
        {
            _seeds.Seed = 5;

            Assert.AreEqual(20261007, new DailyRules().ResolveSeed(_seeds, _clock));
        }

        /// <summary>
        /// Classic and Zen take the seed of the seed source, whatever the date.
        /// </summary>
        [TestCase(GameMode.Classic)]
        [TestCase(GameMode.Zen)]
        public void ResolveSeed_ClassicAndZen_UseTheSeedSource(GameMode mode)
        {
            var rules = GameModeRules.CreateDefault().Get(mode);

            Assert.AreEqual(1234, rules.ResolveSeed(_seeds, _clock));
        }

        /// <summary>
        /// The day key packs the date as yyyyMMdd with zero padding.
        /// </summary>
        [Test]
        public void DayKey_ForASingleDigitMonthAndDay_IsZeroPadded()
        {
            Assert.AreEqual(20260105, DailyRules.DayKey(2026, 1, 5));
        }
    }
}
