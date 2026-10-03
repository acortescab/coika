using System;
using System.Collections.Generic;
using System.Reflection;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the <see cref="ScoreSystem"/> (issue #8): the points of merges, drops and the Supernova, the combo
    /// multiplier, the best score, the overflow, the run stats, the reset, the binding to the events without leaked
    /// handlers, and that handling an event allocates nothing. The scoring is driven through its public methods and
    /// a clock the test moves, so no physics or scene is needed.
    /// </summary>
    public class ScoreSystemTests
    {
        private const int LOOP_COUNT = 1000;
        private const float TOLERANCE = 0.0001f;

        // Merge score of each tier of the game (GDD §3.2), by tier index.
        private static readonly float[] MergeScores = { 1f, 3f, 6f, 10f, 15f, 21f, 28f, 36f, 45f, 55f, 66f };

        private readonly List<UnityEngine.Object> _created = new();
        private readonly List<(int Score, int Delta)> _scoreEvents = new();
        private GameConfig _config;
        private List<TierDefinition> _tiers;
        private ScoreSystem _score;
        private double _now;
        private int _newBestCount;

        /// <summary>
        /// Creates the 11 tiers, a config with the default values (Supernova 500) and a system whose clock the test
        /// moves through <c>_now</c>.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, null);
            _tiers = CreateTiers(MergeScores);
            _now = 0d;
            _score = CreateSystem(_config);
        }

        /// <summary>
        /// Destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _score.Unbind();
            foreach (var created in _created)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
            _scoreEvents.Clear();
            _newBestCount = 0;
            UnityEngine.Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// At multiplier 1, a merge into each tier from 1 to 10 gives 3, 6, 10, 15, 21, 28, 36, 45, 55 and 66.
        /// </summary>
        [Test]
        public void OnMerged_IntoEveryTierAtMultiplierOne_AddsTheMergeScore()
        {
            for (var tier = 1; tier <= 10; tier++)
            {
                _scoreEvents.Clear();

                MergeAt(tier * 2d, tier);

                Assert.AreEqual(1, _scoreEvents.Count, $"Tier {tier}: one event.");
                Assert.AreEqual((int)MergeScores[tier], _scoreEvents[0].Delta, $"Tier {tier}: points.");
            }
        }

        /// <summary>
        /// A drop gives the tier index, with no multiplier even in the middle of a combo.
        /// </summary>
        [Test]
        public void OnPieceDropped_DuringACombo_AddsTheTierIndexWithoutMultiplier()
        {
            MergeAt(0d, 2);
            MergeAt(0.5d, 2);
            Assert.AreEqual(1.25f, _score.ComboTracker.Multiplier, TOLERANCE, "The combo is running.");
            _scoreEvents.Clear();

            _score.OnPieceDropped(4);

            Assert.AreEqual(1, _scoreEvents.Count);
            Assert.AreEqual(4, _scoreEvents[0].Delta);
        }

        /// <summary>
        /// Dropping tier 0 is worth nothing: the score does not change and no event is raised.
        /// </summary>
        [Test]
        public void OnPieceDropped_OfTierZero_ChangesNothingButTheStats()
        {
            _score.OnPieceDropped(0);

            Assert.AreEqual(0, _score.Score);
            Assert.AreEqual(0, _scoreEvents.Count);
            Assert.AreEqual(1, _score.PiecesDropped);
        }

        /// <summary>
        /// Merges at 0, 0.5 and 0.9 s into tier 5 (merge score 21) give 21, 26 and 31, and a merge at 2.5 s gives 21
        /// again. The 26 is the floor of 21 × 1.25.
        /// </summary>
        [Test]
        public void OnMerged_AtTheTimesOfTheIssue_AppliesTheMultiplierAndFloors()
        {
            var deltas = new List<int>();
            foreach (var time in new[] { 0d, 0.5d, 0.9d, 2.5d })
            {
                _scoreEvents.Clear();
                MergeAt(time, 5);
                deltas.Add(_scoreEvents[0].Delta);
            }

            CollectionAssert.AreEqual(new[] { 21, 26, 31, 21 }, deltas);
        }

        /// <summary>
        /// The combo counts the merge being scored: the first merge of a combo is at ×1, the second at ×1.25.
        /// </summary>
        [Test]
        public void OnMerged_RegistersTheComboBeforeCalculatingThePoints()
        {
            MergeAt(0d, 5);
            Assert.AreEqual(1, _score.ComboTracker.Combo);

            MergeAt(0.3d, 5);

            Assert.AreEqual(2, _score.ComboTracker.Combo);
            Assert.AreEqual(21 + 26, _score.Score);
        }

        /// <summary>
        /// After 20 consecutive merges the multiplier stays at ×3, so a merge into tier 10 gives 3 × 66.
        /// </summary>
        [Test]
        public void OnMerged_AfterTwentyConsecutiveMerges_UsesTheCappedMultiplier()
        {
            for (var i = 0; i < 20; i++)
            {
                MergeAt(i * 0.1d, 1);
            }

            _scoreEvents.Clear();
            MergeAt(2.1d, 10);

            Assert.AreEqual(3f, _score.ComboTracker.Multiplier, TOLERANCE);
            Assert.AreEqual(198, _scoreEvents[0].Delta);
        }

        /// <summary>
        /// The Supernova adds the bonus of the config, 500 by default, even in the middle of a combo, and it does not
        /// touch the combo.
        /// </summary>
        [Test]
        public void OnSupernova_DuringACombo_AddsTheBonusWithoutMultiplier()
        {
            MergeAt(0d, 2);
            MergeAt(0.5d, 2);
            var combo = _score.ComboTracker.Combo;
            _scoreEvents.Clear();

            _score.OnSupernova();

            Assert.AreEqual(1, _scoreEvents.Count);
            Assert.AreEqual(500, _scoreEvents[0].Delta);
            Assert.AreEqual(combo, _score.ComboTracker.Combo, "The combo is untouched.");
            Assert.AreEqual(2, _score.Merges, "A Supernova is not a merge.");
        }

        /// <summary>
        /// The Supernova bonus comes from the config.
        /// </summary>
        [Test]
        public void OnSupernova_WithAnotherBonusInTheConfig_AddsThatBonus()
        {
            var score = new ScoreSystem(CreateScoringConfig(750), _tiers, () => _now);

            score.OnSupernova();

            Assert.AreEqual(750, score.Score);
        }

        /// <summary>
        /// The score event carries the new score and the points just added.
        /// </summary>
        [Test]
        public void ScoreChanged_AfterTwoScorings_CarriesTheScoreAndTheDelta()
        {
            _score.OnPieceDropped(3);
            _score.OnPieceDropped(4);

            Assert.AreEqual(2, _scoreEvents.Count);
            Assert.AreEqual((3, 3), _scoreEvents[0]);
            Assert.AreEqual((7, 4), _scoreEvents[1]);
            Assert.AreEqual(7, _score.Score);
        }

        /// <summary>
        /// The new-best event fires once, when the score first goes above the best, and not again in the same run.
        /// </summary>
        [Test]
        public void NewBestReached_WhenTheBestIsExceeded_FiresExactlyOnce()
        {
            _score.BestScore = 10;

            _score.OnPieceDropped(4);
            _score.OnPieceDropped(4);
            Assert.AreEqual(0, _newBestCount, "8 is below 10.");
            Assert.IsFalse(_score.IsNewBest);

            _score.OnPieceDropped(4);
            Assert.AreEqual(1, _newBestCount, "12 is above 10.");
            Assert.IsTrue(_score.IsNewBest);

            _score.OnPieceDropped(4);
            _score.OnSupernova();
            Assert.AreEqual(1, _newBestCount, "No second time in the same run.");
        }

        /// <summary>
        /// Reaching exactly the best score is not beating it.
        /// </summary>
        [Test]
        public void NewBestReached_WhenTheScoreEqualsTheBest_DoesNotFire()
        {
            _score.BestScore = 8;

            _score.OnPieceDropped(4);
            _score.OnPieceDropped(4);

            Assert.AreEqual(8, _score.Score);
            Assert.AreEqual(0, _newBestCount);
            Assert.IsFalse(_score.IsNewBest);
        }

        /// <summary>
        /// A reset clears the flag, so the next run can reach a new best again, and it keeps the best score.
        /// </summary>
        [Test]
        public void ResetForNewRun_AfterANewBest_ClearsTheFlagAndAllowsAnotherOne()
        {
            _score.BestScore = 5;
            _score.OnPieceDropped(4);
            _score.OnPieceDropped(4);
            Assert.IsTrue(_score.IsNewBest);

            _score.ResetForNewRun();

            Assert.IsFalse(_score.IsNewBest);
            Assert.AreEqual(5, _score.BestScore, "The best score is kept.");

            _score.OnPieceDropped(4);
            _score.OnPieceDropped(4);
            Assert.AreEqual(2, _newBestCount, "Once per run.");
        }

        /// <summary>
        /// With the default best score of 0, the first points of a run already beat it.
        /// </summary>
        [Test]
        public void NewBestReached_WithTheDefaultBestScore_FiresOnTheFirstPoints()
        {
            _score.OnPieceDropped(1);

            Assert.AreEqual(1, _newBestCount);
            Assert.IsTrue(_score.IsNewBest);
        }

        /// <summary>
        /// The system never writes the best score: only whoever owns it sets it.
        /// </summary>
        [Test]
        public void BestScore_AfterARunThatBeatsIt_IsNotChanged()
        {
            _score.BestScore = 7;

            _score.OnPieceDropped(4);
            _score.OnPieceDropped(4);
            _score.OnSupernova();

            Assert.Greater(_score.Score, 7);
            Assert.AreEqual(7, _score.BestScore);
        }

        /// <summary>
        /// A score at the top of the range stops there: it does not wrap to negative, and no event is raised for
        /// points that were not added.
        /// </summary>
        [Test]
        public void Score_WithExtremeBonuses_StopsAtIntMaxValue()
        {
            var score = CreateSystem(CreateScoringConfig(int.MaxValue));
            score.OnPieceDropped(4);

            score.OnSupernova();
            Assert.AreEqual(int.MaxValue, score.Score, "Stops at the top.");
            Assert.AreEqual(int.MaxValue - 4, _scoreEvents[^1].Delta, "Only the points that fit are reported.");
            var events = _scoreEvents.Count;

            score.OnSupernova();
            score.OnPieceDropped(4);

            Assert.AreEqual(int.MaxValue, score.Score, "Does not wrap.");
            Assert.AreEqual(events, _scoreEvents.Count, "Nothing was added, so nothing is raised.");
        }

        /// <summary>
        /// A merge score so large that the multiplied value does not fit in an int is limited, not wrapped.
        /// </summary>
        [Test]
        public void OnMerged_WithAnEnormousMergeScore_IsLimitedToIntMaxValue()
        {
            var huge = CreateTiers(new[] { 0f, 1e12f });
            var score = new ScoreSystem(_config, huge, () => _now);

            Assert.AreEqual(int.MaxValue, score.CalculateMergeScore(1, 3f));
            score.OnMerged(1);

            Assert.AreEqual(int.MaxValue, score.Score);
        }

        /// <summary>
        /// A merge score or multiplier that is zero or negative adds nothing: the score never goes down.
        /// </summary>
        [Test]
        public void CalculateMergeScore_WithZeroOrNegativeInputs_ReturnsZero()
        {
            var odd = CreateTiers(new[] { 0f, -5f, 7f });
            var score = new ScoreSystem(_config, odd, () => _now);

            Assert.AreEqual(0, score.CalculateMergeScore(1, 2f), "Negative merge score");
            Assert.AreEqual(0, score.CalculateMergeScore(2, 0f), "Zero multiplier");
            Assert.AreEqual(0, score.CalculateMergeScore(2, -1f), "Negative multiplier");
        }

        /// <summary>
        /// The one formula, floored: 21 × 1.25 is 26, and the table of the GDD holds at ×1.
        /// </summary>
        [Test]
        public void CalculateMergeScore_WithMultipliers_FloorsTheProduct()
        {
            Assert.AreEqual(26, _score.CalculateMergeScore(5, 1.25f));
            Assert.AreEqual(31, _score.CalculateMergeScore(5, 1.5f));
            Assert.AreEqual(198, _score.CalculateMergeScore(10, 3f));
            for (var tier = 1; tier <= 10; tier++)
            {
                Assert.AreEqual((int)MergeScores[tier], _score.CalculateMergeScore(tier, 1f), $"Tier {tier}");
            }
        }

        /// <summary>
        /// A tier that does not exist is a programming error and throws, without changing the score or the combo.
        /// </summary>
        [Test]
        public void OnMerged_WithAnUnknownTier_ThrowsAndChangesNothing()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _score.OnMerged(11), "Too high");
            Assert.Throws<ArgumentOutOfRangeException>(() => _score.OnMerged(-1), "Negative");
            Assert.Throws<ArgumentOutOfRangeException>(() => _score.CalculateMergeScore(11, 1f));

            Assert.AreEqual(0, _score.Score);
            Assert.AreEqual(0, _score.Merges);
            Assert.AreEqual(0, _score.ComboTracker.Combo);
        }

        /// <summary>
        /// The Game Over stats follow the run: merges, drops, the highest tier and the longest combo.
        /// </summary>
        [Test]
        public void Stats_AfterAMixedRun_AreCounted()
        {
            _score.OnPieceDropped(3);
            _score.OnPieceDropped(1);
            MergeAt(0d, 2);
            MergeAt(0.4d, 6);
            MergeAt(0.8d, 3);
            MergeAt(5d, 4);
            _score.OnSupernova();

            Assert.AreEqual(4, _score.Merges);
            Assert.AreEqual(2, _score.PiecesDropped);
            Assert.AreEqual(6, _score.HighestTierReached);
            Assert.AreEqual(3, _score.MaxCombo, "The combo of 3 was longer than the later one.");
        }

        /// <summary>
        /// A dropped piece counts for the highest tier too, so a run without merges does not report tier 0.
        /// </summary>
        [Test]
        public void HighestTierReached_WithOnlyDrops_IsTheHighestDrop()
        {
            _score.OnPieceDropped(1);
            _score.OnPieceDropped(4);
            _score.OnPieceDropped(2);

            Assert.AreEqual(4, _score.HighestTierReached);
        }

        /// <summary>
        /// A reset clears the score, the combo and the stats, keeps the best score, and raises no event, not even
        /// the new-best one when the score of the run was above the best.
        /// </summary>
        [Test]
        public void ResetForNewRun_AfterARunAboveTheBest_ClearsTheRunWithoutEvents()
        {
            _score.BestScore = 10;
            _score.OnPieceDropped(4);
            MergeAt(0d, 5);
            MergeAt(0.3d, 5);
            Assert.IsTrue(_score.IsNewBest, "The run went above the best, so the reset has a flag to clear.");
            var newBestBefore = _newBestCount;
            var comboEvents = 0;
            _score.ComboTracker.ComboChanged += (_, _) => comboEvents++;
            _scoreEvents.Clear();

            _score.ResetForNewRun();

            Assert.AreEqual(0, _score.Score);
            Assert.IsFalse(_score.IsNewBest);
            Assert.AreEqual(0, _score.ComboTracker.Combo);
            Assert.AreEqual(1f, _score.ComboTracker.Multiplier, TOLERANCE);
            Assert.AreEqual(0, _score.Merges);
            Assert.AreEqual(0, _score.PiecesDropped);
            Assert.AreEqual(0, _score.HighestTierReached);
            Assert.AreEqual(0, _score.MaxCombo);
            Assert.AreEqual(10, _score.BestScore);
            Assert.AreEqual(0, _scoreEvents.Count, "Score events");
            Assert.AreEqual(0, comboEvents, "Combo events");
            Assert.AreEqual(newBestBefore, _newBestCount, "New best events");
        }

        /// <summary>
        /// The combo of the previous run does not carry over: a merge right after a reset is worth ×1, even though
        /// the last merge before it was inside the window.
        /// </summary>
        [Test]
        public void ResetForNewRun_ThenAMergeSoonAfter_StartsTheComboAtOne()
        {
            MergeAt(0d, 5);
            _now = 0.2d;
            _score.ResetForNewRun();
            _scoreEvents.Clear();

            MergeAt(0.4d, 5);

            Assert.AreEqual(1, _score.ComboTracker.Combo);
            Assert.AreEqual(21, _scoreEvents[0].Delta);
        }

        /// <summary>
        /// A tick after the window ends the combo, so the next merge is back at ×1.
        /// </summary>
        [Test]
        public void Tick_AfterTheWindow_EndsTheComboAndRaisesTheEvent()
        {
            var comboEvents = new List<(int Combo, float Multiplier)>();
            _score.ComboTracker.ComboChanged += (combo, multiplier) => comboEvents.Add((combo, multiplier));
            MergeAt(0d, 5);
            MergeAt(0.5d, 5);

            _now = 3d;
            _score.Tick();

            Assert.AreEqual(0, _score.ComboTracker.Combo);
            Assert.AreEqual((0, 1f), comboEvents[^1], "The last combo event is the end of the combo.");
        }

        /// <summary>
        /// A missing dependency is a programming error and throws.
        /// </summary>
        [Test]
        public void Constructor_WithNullDependencies_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ScoreSystem(null, _tiers, () => _now), "config");
            Assert.Throws<ArgumentNullException>(() => new ScoreSystem(_config, null, () => _now), "tiers");
            Assert.Throws<ArgumentNullException>(() => new ScoreSystem(_config, _tiers, null), "clock");
        }

        /// <summary>
        /// Binding adds exactly one listener to each event, and binding again does not add a second one.
        /// </summary>
        [Test]
        public void Bind_Twice_LeavesOneListenerPerEvent()
        {
            var (merge, drop) = CreateSources();

            _score.Bind(merge, drop);
            _score.Bind(merge, drop);

            AssertListeners(merge, drop, 1);
        }

        /// <summary>
        /// Unbinding removes every listener, and doing it twice, or without ever binding, is harmless.
        /// </summary>
        [Test]
        public void Unbind_AfterBind_RemovesEveryListener()
        {
            var (merge, drop) = CreateSources();
            _score.Bind(merge, drop);

            _score.Unbind();
            _score.Unbind();

            AssertListeners(merge, drop, 0);
        }

        /// <summary>
        /// Binding to new sources releases the old ones, so a replaced merge system cannot score any more.
        /// </summary>
        [Test]
        public void Bind_ToNewSources_ReleasesTheOldOnes()
        {
            var (oldMerge, oldDrop) = CreateSources();
            var (newMerge, newDrop) = CreateSources();
            _score.Bind(oldMerge, oldDrop);

            _score.Bind(newMerge, newDrop);

            AssertListeners(oldMerge, oldDrop, 0);
            AssertListeners(newMerge, newDrop, 1);
        }

        /// <summary>
        /// A reset keeps the binding and leaks nothing: still one listener per event, and the events still score.
        /// </summary>
        [Test]
        public void ResetForNewRun_WhileBound_KeepsExactlyOneListenerPerEvent()
        {
            var (merge, drop) = CreateSources();
            _score.Bind(merge, drop);

            _score.ResetForNewRun();
            _score.ResetForNewRun();

            AssertListeners(merge, drop, 1);
            Raise(drop, "PieceDropped", 4);
            Assert.AreEqual(4, _score.Score, "The drop still scores after the resets.");
        }

        /// <summary>
        /// The bound events score: a merge, a Supernova and a drop each add their points.
        /// </summary>
        [Test]
        public void Bind_WhenTheSourcesRaiseEvents_ScoresThem()
        {
            var (merge, drop) = CreateSources();
            _score.Bind(merge, drop);

            Raise(drop, "PieceDropped", 3);
            Raise(merge, "Merged", 5, Vector2.zero, Vector2.zero);
            Raise(merge, "SupernovaTriggered", Vector2.zero);

            Assert.AreEqual(3 + 21 + 500, _score.Score);
            Assert.AreEqual(1, _score.Merges);
            Assert.AreEqual(1, _score.PiecesDropped);
        }

        /// <summary>
        /// After unbinding, the old sources no longer score.
        /// </summary>
        [Test]
        public void Unbind_ThenTheSourcesRaiseEvents_ScoresNothing()
        {
            var (merge, drop) = CreateSources();
            _score.Bind(merge, drop);
            _score.Unbind();

            Raise(drop, "PieceDropped", 4);
            Raise(merge, "Merged", 5, Vector2.zero, Vector2.zero);
            Raise(merge, "SupernovaTriggered", Vector2.zero);

            Assert.AreEqual(0, _score.Score);
            Assert.AreEqual(0, _score.Merges);
        }

        /// <summary>
        /// A missing source is a programming error and throws, and it binds nothing.
        /// </summary>
        [Test]
        public void Bind_WithANullSource_Throws()
        {
            var (merge, drop) = CreateSources();

            Assert.Throws<ArgumentNullException>(() => _score.Bind(null, drop), "merge system");
            Assert.Throws<ArgumentNullException>(() => _score.Bind(merge, null), "drop controller");
            AssertListeners(merge, drop, 0);
        }

        /// <summary>
        /// Unbinding after the sources were destroyed does not throw.
        /// </summary>
        [Test]
        public void Unbind_AfterTheSourcesWereDestroyed_DoesNotThrow()
        {
            var (merge, drop) = CreateSources();
            _score.Bind(merge, drop);
            UnityEngine.Object.DestroyImmediate(merge.gameObject);
            UnityEngine.Object.DestroyImmediate(drop.gameObject);

            Assert.DoesNotThrow(() => _score.Unbind());
        }

        /// <summary>
        /// Scoring merges, drops and the Supernova, ticking and resetting allocate nothing in steady state.
        /// </summary>
        [Test]
        public void Scoring_InSteadyState_AllocatesNothing()
        {
            // A system of its own: the one of the other tests records every event in a list, which grows.
            var score = CreateQuietSystem();
            score.OnMerged(3);
            score.OnPieceDropped(2);
            score.OnSupernova();
            score.Tick();
            score.ResetForNewRun();

            var allocated = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < LOOP_COUNT; i++)
                {
                    _now = i * 0.3d;
                    score.OnMerged(1 + i % 10);
                    score.OnPieceDropped(i % 5);
                    score.OnSupernova();
                    score.Tick();
                    if (i % 100 == 99)
                    {
                        score.ResetForNewRun();
                    }
                }
            });

            Assert.LessOrEqual(allocated, AllocationMeter.TOLERANCE_COUNT, "Managed allocations made by the score system.");
        }

        /// <summary>
        /// The path the game uses, the bound events with their cached adapters, allocates nothing either.
        /// </summary>
        [Test]
        public void BoundEvents_InSteadyState_AllocateNothing()
        {
            var score = CreateQuietSystem();
            var (merge, drop) = CreateSources();
            score.Bind(merge, drop);
            var onMerged = Handler<Action<int, Vector2, Vector2>>(merge, "Merged");
            var onSupernova = Handler<Action<Vector2>>(merge, "SupernovaTriggered");
            var onDropped = Handler<Action<int>>(drop, "PieceDropped");
            onMerged(3, Vector2.zero, Vector2.zero);
            onSupernova(Vector2.zero);
            onDropped(2);
            score.ResetForNewRun();

            var allocated = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < LOOP_COUNT; i++)
                {
                    _now = i * 0.3d;
                    onMerged(1 + i % 10, Vector2.zero, Vector2.zero);
                    onDropped(i % 5);
                    onSupernova(Vector2.zero);
                    if (i % 100 == 99)
                    {
                        score.ResetForNewRun();
                    }
                }
            });

            score.Unbind();
            Assert.LessOrEqual(allocated, AllocationMeter.TOLERANCE_COUNT, "Managed allocations made by the bound events.");
        }

        /// <summary>
        /// Creates a system with a clock the test moves, and records its events.
        /// </summary>
        private ScoreSystem CreateSystem(GameConfig config)
        {
            var score = new ScoreSystem(config, _tiers, () => _now);
            score.ScoreChanged += (total, delta) => _scoreEvents.Add((total, delta));
            score.NewBestReached += () => _newBestCount++;
            return score;
        }

        /// <summary>
        /// Creates a system with no recorded events and a best score no run reaches, for the allocation tests.
        /// </summary>
        private ScoreSystem CreateQuietSystem()
        {
            var score = new ScoreSystem(_config, _tiers, () => _now);
            score.ScoreChanged += (_, _) => { };
            score.ComboTracker.ComboChanged += (_, _) => { };
            score.BestScore = int.MaxValue;
            return score;
        }

        /// <summary>
        /// Creates a config with the default combo values and the given Supernova bonus, and destroys it at the end of
        /// the test.
        /// </summary>
        private GameConfig CreateScoringConfig(int superNovaBonus)
        {
            var config = TestGameConfig.CreateWithScoring(1f, 0.25f, 3, superNovaBonus);
            _created.Add(config);
            return config;
        }

        /// <summary>
        /// Moves the clock to a time and scores a merge into a tier.
        /// </summary>
        private void MergeAt(double time, int tier)
        {
            _now = time;
            _score.OnMerged(tier);
        }

        /// <summary>
        /// Creates in-memory tiers with the given merge scores, in tier order.
        /// </summary>
        private List<TierDefinition> CreateTiers(float[] mergeScores)
        {
            var tiers = new List<TierDefinition>();
            for (var i = 0; i < mergeScores.Length; i++)
            {
                var tier = PieceFixtures.CreateTier(i, 1f, _created);
                var serializedTier = new SerializedObject(tier);
                serializedTier.FindProperty("_mergeScore").floatValue = mergeScores[i];
                serializedTier.ApplyModifiedPropertiesWithoutUndo();
                tiers.Add(tier);
            }

            return tiers;
        }

        /// <summary>
        /// Creates a merge system and a drop controller that are not initialized, only to carry the events.
        /// </summary>
        private (MergeSystem Merge, DropController Drop) CreateSources()
        {
            var merge = new GameObject("MergeSystem").AddComponent<MergeSystem>();
            var drop = new GameObject("DropController").AddComponent<DropController>();
            _created.Add(merge.gameObject);
            _created.Add(drop.gameObject);
            return (merge, drop);
        }

        /// <summary>
        /// Checks how many listeners each of the three events has.
        /// </summary>
        private static void AssertListeners(MergeSystem merge, DropController drop, int expected)
        {
            Assert.AreEqual(expected, ListenerCount(merge, "Merged"), "Merged");
            Assert.AreEqual(expected, ListenerCount(merge, "SupernovaTriggered"), "SupernovaTriggered");
            Assert.AreEqual(expected, ListenerCount(drop, "PieceDropped"), "PieceDropped");
        }

        /// <summary>
        /// Number of listeners of an event, read from the field the compiler makes for it. A C# event can only be
        /// raised from inside its class, so the tests reach it by reflection.
        /// </summary>
        private static int ListenerCount(object source, string eventName)
        {
            var handler = (Delegate)EventField(source, eventName).GetValue(source);
            return handler == null ? 0 : handler.GetInvocationList().Length;
        }

        /// <summary>
        /// Raises an event of a source by reflection, as its class would. It allocates, so the allocation tests use
        /// <see cref="Handler{T}"/> instead.
        /// </summary>
        private static void Raise(object source, string eventName, params object[] arguments)
        {
            var handler = (Delegate)EventField(source, eventName).GetValue(source);
            handler?.DynamicInvoke(arguments);
        }

        /// <summary>
        /// The delegate behind an event, to call it directly without reflection in the measured code.
        /// </summary>
        private static T Handler<T>(object source, string eventName)
            where T : Delegate
        {
            return (T)EventField(source, eventName).GetValue(source);
        }

        /// <summary>
        /// The field behind a C# event.
        /// </summary>
        private static FieldInfo EventField(object source, string eventName)
        {
            return source.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance);
        }
    }
}
