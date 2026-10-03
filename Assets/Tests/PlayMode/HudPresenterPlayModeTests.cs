using System.Collections.Generic;
using Coika.Data;
using Coika.Gameplay;
using Coika.UI;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the HUD (issue #10): the <see cref="HudPresenter"/> turns the events of the score, the combo and the
    /// spawn queue into what the <see cref="HudView"/> shows, with the real gameplay objects and no scene, and
    /// showing a score allocates nothing.
    /// </summary>
    public class HudPresenterPlayModeTests
    {
        private readonly List<Object> _created = new();
        private UiTestViews _views;
        private GameConfig _config;
        private ScoreSystem _score;
        private HudPresenter _presenter;
        private Sprite[] _sprites;

        /// <summary>
        /// Builds the views, a default config, 11 tiers, a score system with a fixed clock and one sprite per tier.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _views = new UiTestViews();
            _config = ScriptableObject.CreateInstance<GameConfig>();

            var tiers = new List<TierDefinition>();
            _sprites = new Sprite[ThemeDefinition.TIER_COUNT];
            for (var i = 0; i < ThemeDefinition.TIER_COUNT; i++)
            {
                var tier = ScriptableObject.CreateInstance<TierDefinition>();
                tiers.Add(tier);
                _created.Add(tier);

                var texture = new Texture2D(4, 4);
                _created.Add(texture);
                _sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
                _created.Add(_sprites[i]);
            }

            _score = new ScoreSystem(_config, tiers, () => 0d);
            _presenter = new HudPresenter(_views.Hud, _score, tier => _sprites[tier]);
        }

        /// <summary>
        /// Disposes the presenter and destroys everything the test created.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            _views.Destroy();
            foreach (var created in _created)
            {
                Object.Destroy(created);
            }

            _created.Clear();
            Object.Destroy(_config);
        }

        /// <summary>
        /// A new presenter shows the score 0, the best score and no combo.
        /// </summary>
        [Test]
        public void Constructor_ShowsTheInitialValues()
        {
            Assert.That(UiTestViews.Shown(_views.HudScore), Is.EqualTo("0"));
            Assert.That(UiTestViews.Shown(_views.HudBest), Is.EqualTo("0"));
            Assert.That(_views.HudCombo.gameObject.activeSelf, Is.False);
        }

        /// <summary>
        /// A drop changes the score and the label follows.
        /// </summary>
        [Test]
        public void ScoreChanged_AfterADrop_UpdatesTheScoreLabel()
        {
            _score.OnPieceDropped(4);

            Assert.That(UiTestViews.Shown(_views.HudScore), Is.EqualTo("4"));
        }

        /// <summary>
        /// The best score is read from the score system when the HUD refreshes.
        /// </summary>
        [Test]
        public void Refresh_AfterTheBestScoreChanged_ShowsTheBestScore()
        {
            _score.BestScore = 1234;

            _presenter.Refresh();

            Assert.That(UiTestViews.Shown(_views.HudBest), Is.EqualTo("1234"));
        }

        /// <summary>
        /// The first merge of a combo has no bonus, so the label stays hidden; the second shows x1.25.
        /// </summary>
        [Test]
        public void ComboChanged_AfterTwoMerges_ShowsTheMultiplier()
        {
            _score.OnMerged(1);
            Assert.That(_views.HudCombo.gameObject.activeSelf, Is.False);

            _score.OnMerged(1);

            Assert.That(_views.HudCombo.gameObject.activeSelf, Is.True);
            Assert.That(UiTestViews.Shown(_views.HudCombo), Is.EqualTo("x1.25"));
        }

        /// <summary>
        /// When the combo ends the label hides again.
        /// </summary>
        [Test]
        public void ComboChanged_WhenTheComboExpires_HidesTheLabel()
        {
            _score.OnMerged(1);
            _score.OnMerged(1);
            Assert.That(_views.HudCombo.gameObject.activeSelf, Is.True);

            _score.ComboTracker.Tick(100d);

            Assert.That(_views.HudCombo.gameObject.activeSelf, Is.False);
        }

        /// <summary>
        /// The preview shows the sprite of the tier that the queue will drop next, and follows the queue.
        /// </summary>
        [Test]
        public void BindQueue_ThenAdvance_PreviewShowsTheNextTier()
        {
            var queue = new SpawnQueue(_config, 1234);
            _presenter.BindQueue(queue);
            Assert.That(_views.HudNext.sprite, Is.SameAs(_sprites[queue.Next]));

            queue.Advance();

            Assert.That(_views.HudNext.sprite, Is.SameAs(_sprites[queue.Next]));
        }

        /// <summary>
        /// A queue that was replaced no longer drives the preview.
        /// </summary>
        [Test]
        public void BindQueue_WithANewQueue_IgnoresTheOldOne()
        {
            var oldQueue = new SpawnQueue(_config, 1);
            var newQueue = new SpawnQueue(_config, 2);
            _presenter.BindQueue(oldQueue);
            _presenter.BindQueue(newQueue);
            var shown = _views.HudNext.sprite;

            for (var i = 0; i < 20; i++)
            {
                oldQueue.Advance();
            }

            Assert.That(_views.HudNext.sprite, Is.SameAs(shown));
            Assert.That(shown, Is.SameAs(_sprites[newQueue.Next]));
        }

        /// <summary>
        /// After Dispose the presenter no longer reacts to the score.
        /// </summary>
        [Test]
        public void Dispose_StopsFollowingTheScore()
        {
            _presenter.Dispose();

            _score.OnPieceDropped(9);

            Assert.That(UiTestViews.Shown(_views.HudScore), Is.EqualTo("0"));
        }

        /// <summary>
        /// The pause button is a visible placeholder that cannot be pressed in M1.
        /// </summary>
        [Test]
        public void PauseButton_InM1_IsNotInteractable()
        {
            Assert.That(_views.HudPause.interactable, Is.False);
        }

        /// <summary>
        /// Showing a new score and combo allocates nothing (S-50).
        /// </summary>
        [Test]
        public void ScoreChanged_WhileShowingScoreAndCombo_AllocatesNothing()
        {
            _score.OnPieceDropped(1);
            _score.OnMerged(1);
            _score.OnMerged(1);

            var allocations = AllocationMeter.Measure(() =>
            {
                _score.OnPieceDropped(3);
                _score.ComboTracker.RegisterMerge(0d);
            });

            Assert.That(allocations, Is.LessThanOrEqualTo(AllocationMeter.TOLERANCE_COUNT));
        }
    }
}
