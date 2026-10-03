using System.Collections.Generic;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks that <see cref="RunSummary.From"/> (issue #10) copies the stats of the <see cref="ScoreSystem"/> and
    /// reports the best score the player has after the run.
    /// </summary>
    public class RunSummaryTests
    {
        private const float DURATION = 125.5f;

        private readonly List<Object> _created = new();
        private GameConfig _config;
        private ScoreSystem _score;

        /// <summary>
        /// Creates a config, 11 empty tiers and a score system with a fixed clock.
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
        /// The summary carries the score, the stats and the duration of the run.
        /// </summary>
        [Test]
        public void From_AfterARun_CopiesTheStats()
        {
            _score.OnPieceDropped(2);
            _score.OnPieceDropped(4);
            _score.OnMerged(5);

            var summary = RunSummary.From(_score, DURATION);

            Assert.That(summary.Score, Is.EqualTo(_score.Score));
            Assert.That(summary.PiecesDropped, Is.EqualTo(2));
            Assert.That(summary.Merges, Is.EqualTo(1));
            Assert.That(summary.HighestTier, Is.EqualTo(5));
            Assert.That(summary.MaxCombo, Is.EqualTo(1));
            Assert.That(summary.DurationSeconds, Is.EqualTo(DURATION));
        }

        /// <summary>
        /// A run that did not beat the best keeps the old best and is not a new best.
        /// </summary>
        [Test]
        public void From_BelowTheBest_KeepsTheBestAndIsNotNewBest()
        {
            _score.BestScore = 1000;
            _score.OnPieceDropped(3);

            var summary = RunSummary.From(_score, DURATION);

            Assert.That(summary.IsNewBest, Is.False);
            Assert.That(summary.BestScore, Is.EqualTo(1000));
        }

        /// <summary>
        /// A run that beat the best is a new best, and the best score becomes the final score.
        /// </summary>
        [Test]
        public void From_AboveTheBest_IsNewBestWithTheScoreAsBest()
        {
            _score.BestScore = 2;
            _score.OnPieceDropped(7);

            var summary = RunSummary.From(_score, DURATION);

            Assert.That(summary.IsNewBest, Is.True);
            Assert.That(summary.BestScore, Is.EqualTo(summary.Score));
            Assert.That(summary.Score, Is.EqualTo(7));
        }
    }
}
