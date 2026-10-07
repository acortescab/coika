using Coika.Core;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the best score per mode in the save (issue #66): the round trip, no leak between modes, the Daily day
    /// check, and that an older file still loads.
    /// </summary>
    public class SaveDataBestTests
    {
        private const int TODAY = 20261007;
        private const int YESTERDAY = 20261006;

        /// <summary>
        /// Each mode reads back its own best after the save is written as JSON and read again.
        /// </summary>
        [Test]
        public void SetBest_ForEveryMode_RoundTripsThroughJson()
        {
            var data = new SaveData();
            data.SetBest(GameMode.Classic, 100);
            data.SetBest(GameMode.Daily, 200, TODAY);
            data.SetBest(GameMode.Zen, 300);

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            Assert.AreEqual(100, loaded.GetBest(GameMode.Classic));
            Assert.AreEqual(200, loaded.GetBest(GameMode.Daily, TODAY));
            Assert.AreEqual(300, loaded.GetBest(GameMode.Zen));
        }

        /// <summary>
        /// Setting the best of one mode leaves the others at 0.
        /// </summary>
        [TestCase(GameMode.Classic)]
        [TestCase(GameMode.Daily)]
        [TestCase(GameMode.Zen)]
        public void SetBest_OneMode_DoesNotChangeTheOthers(GameMode mode)
        {
            var data = new SaveData();

            data.SetBest(mode, 500, TODAY);

            foreach (GameMode other in System.Enum.GetValues(typeof(GameMode)))
            {
                Assert.AreEqual(other == mode ? 500 : 0, data.GetBest(other, TODAY), $"{other} after setting {mode}");
            }
        }

        /// <summary>
        /// A Daily best set yesterday reads as 0 today.
        /// </summary>
        [Test]
        public void GetBest_DailySetOnAnotherDay_ReadsZero()
        {
            var data = new SaveData();
            data.SetBest(GameMode.Daily, 400, YESTERDAY);

            Assert.AreEqual(0, data.GetBest(GameMode.Daily, TODAY));
            Assert.AreEqual(400, data.GetBest(GameMode.Daily, YESTERDAY));
        }

        /// <summary>
        /// A Daily score of a new day replaces the old day's score, even when the old one was higher.
        /// </summary>
        [Test]
        public void SetBest_DailyOnANewDay_ReplacesTheOldDay()
        {
            var data = new SaveData();
            data.SetBest(GameMode.Daily, 900, YESTERDAY);

            data.SetBest(GameMode.Daily, 50, TODAY);

            Assert.AreEqual(50, data.GetBest(GameMode.Daily, TODAY));
        }

        /// <summary>
        /// A lower score never lowers the best.
        /// </summary>
        [TestCase(GameMode.Classic)]
        [TestCase(GameMode.Daily)]
        [TestCase(GameMode.Zen)]
        public void SetBest_ALowerScore_KeepsTheBest(GameMode mode)
        {
            var data = new SaveData();
            data.SetBest(mode, 300, TODAY);

            data.SetBest(mode, 100, TODAY);

            Assert.AreEqual(300, data.GetBest(mode, TODAY));
        }

        /// <summary>
        /// A mode that does not record the best still folds the run into the totals.
        /// </summary>
        [Test]
        public void RecordRun_WithoutRecordingTheBest_KeepsTheBestAndCountsTheRun()
        {
            var data = new SaveData();

            data.RecordRun(900, 4, 10, 30f, GameMode.Zen, false, TODAY);

            Assert.AreEqual(0, data.GetBest(GameMode.Zen));
            Assert.AreEqual(1, data.totals.games);
            Assert.AreEqual(4, data.highestTier);
        }

        /// <summary>
        /// A run in Daily writes the Daily best under today's key, not the Classic one.
        /// </summary>
        [Test]
        public void RecordRun_InDaily_WritesTheDailyBestOnly()
        {
            var data = new SaveData();

            data.RecordRun(250, 2, 5, 12f, GameMode.Daily, true, TODAY);

            Assert.AreEqual(250, data.GetBest(GameMode.Daily, TODAY));
            Assert.AreEqual(0, data.GetBest(GameMode.Classic));
        }

        /// <summary>
        /// A file written before modes existed has no Zen score and an empty Daily one: it loads and keeps its Classic best.
        /// </summary>
        [Test]
        public void FromJson_AFileFromBeforeModes_KeepsTheClassicBest()
        {
            const string json = "{\"version\":1,\"bestScore\":{\"classic\":1234}}";

            var data = JsonUtility.FromJson<SaveData>(json);
            data.Normalize();

            Assert.AreEqual(1, data.version);
            Assert.AreEqual(1234, data.GetBest(GameMode.Classic));
            Assert.AreEqual(0, data.GetBest(GameMode.Zen));
            Assert.AreEqual(0, data.GetBest(GameMode.Daily, TODAY));
        }

        /// <summary>
        /// A value outside the enum has no best score field and is an error, not Classic's score.
        /// </summary>
        [Test]
        public void GetBest_ForAnUnknownMode_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SaveData().GetBest((GameMode)9));
        }
    }
}
