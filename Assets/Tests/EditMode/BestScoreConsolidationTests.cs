using System.Collections.Generic;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks that the best score is consolidated in the save only when a run ends (issue #26): beating the
    /// record in memory during a run, then saving as the app pauses, must not persist the new record.
    /// </summary>
    public class BestScoreConsolidationTests
    {
        private const int PREVIOUS_BEST = 1;

        private readonly List<Object> _created = new();
        private GameConfig _config;
        private ScoreSystem _score;
        private FakeSaveStorage _storage;
        private SaveSystem _save;

        /// <summary>
        /// Creates a score system and a save whose stored best is <see cref="PREVIOUS_BEST"/>.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, null);
            var tiers = new List<TierDefinition>();
            for (var i = 0; i < ThemeDefinition.TIER_COUNT; i++)
            {
                tiers.Add(PieceFixtures.CreateTier(i, 1f + i, _created));
            }

            _score = new ScoreSystem(_config, tiers, () => 0d);
            _storage = new FakeSaveStorage();
            _save = new SaveSystem(_storage);
            _save.Data.bestScore.classic = PREVIOUS_BEST;
            _save.Save();
            _score.BestScore = PREVIOUS_BEST;
        }

        /// <summary>
        /// Destroys everything the test created.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Saving mid-run, as on app pause, keeps the previous best even when the run already beats it.
        /// </summary>
        [Test]
        public void Save_MidRunBeatingRecord_PersistsPreviousBest()
        {
            _score.OnMerged(5);
            Assert.That(_score.Score, Is.GreaterThan(PREVIOUS_BEST), "the run must beat the record for this test to mean anything");

            _save.Save();
            var reloaded = new SaveSystem(_storage);
            reloaded.Load();

            Assert.That(reloaded.Data.bestScore.classic, Is.EqualTo(PREVIOUS_BEST));
        }

        /// <summary>
        /// Recording the finished run consolidates the new best.
        /// </summary>
        [Test]
        public void RecordRun_AfterBeatingRecord_PersistsNewBest()
        {
            _score.OnMerged(5);
            var summary = RunSummary.From(_score, 10f);

            _save.Data.RecordRun(summary.Score, summary.HighestTier, summary.Merges, summary.DurationSeconds);
            _save.Save();
            var reloaded = new SaveSystem(_storage);
            reloaded.Load();

            Assert.That(reloaded.Data.bestScore.classic, Is.EqualTo(_score.Score));
        }
    }
}
