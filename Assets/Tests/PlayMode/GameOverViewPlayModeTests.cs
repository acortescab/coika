using System.Collections;
using Coika.Gameplay;
using Coika.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the Game Over screen (issue #10): the <see cref="GameOverView"/> shows the values of a fake
    /// <see cref="RunSummary"/>, the "NEW BEST!" banner only when it applies, Retry raises one event, and the
    /// fade-in runs on unscaled time. The <see cref="GameOverPresenter"/> forwards the click and the tier icon.
    /// </summary>
    public class GameOverViewPlayModeTests
    {
        private UiTestViews _views;
        private double _now;

        /// <summary>
        /// Builds the views; the Game Over object starts inactive, as in the prefab. The view runs on a fake clock.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _now = 0d;
            _views = new UiTestViews();
            _views.GameOver.Initialize(() => _now);
        }

        /// <summary>
        /// Moves the fake clock forward and waits a frame, so the view's Update sees the new time.
        /// </summary>
        private IEnumerator Advance(double seconds)
        {
            _now += seconds;
            yield return null;
        }

        /// <summary>
        /// Restores the time scale and destroys the views.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            _views.Destroy();
        }

        /// <summary>
        /// Builds a summary with defaults for the values a test does not care about.
        /// </summary>
        private static RunSummary Summary(int score = 1, int best = 1, bool isNewBest = false, int highestTier = 0, int piecesDropped = 1, float duration = 1f)
        {
            return new RunSummary(score, best, isNewBest, highestTier, piecesDropped, duration);
        }

        /// <summary>
        /// Show activates the view and writes the score, the best score, the pieces and the time.
        /// </summary>
        [UnityTest]
        public IEnumerator Show_WithASummary_ShowsItsValues()
        {
            _views.GameOver.Show(Summary(score: 4321, best: 5000, piecesDropped: 87, duration: 125.5f));
            yield return Advance(UiAnimation.COUNT_UP_SECONDS);

            Assert.That(_views.GameOverObject.activeSelf, Is.True);
            Assert.That(UiTestViews.Shown(_views.OverScore), Is.EqualTo("4321"));
            Assert.That(UiTestViews.Shown(_views.OverBest), Is.EqualTo("5000"));
            Assert.That(UiTestViews.Shown(_views.OverPieces), Is.EqualTo("87"));
            Assert.That(UiTestViews.Shown(_views.OverTime), Is.EqualTo("02:05"));
        }

        /// <summary>
        /// The banner is visible for a new best score and hidden again for a later run that is not one.
        /// </summary>
        [Test]
        public void Show_BannerFollowsIsNewBest()
        {
            _views.GameOver.Show(Summary(score: 900, best: 900, isNewBest: true));
            Assert.That(_views.OverNewBest.activeSelf, Is.True);

            _views.GameOver.Show(Summary(score: 100, best: 900));

            Assert.That(_views.OverNewBest.activeSelf, Is.False);
        }

        /// <summary>
        /// Minutes and seconds roll over correctly, both padded to two digits (mm:ss).
        /// </summary>
        [TestCase(0f, "00:00")]
        [TestCase(59.9f, "00:59")]
        [TestCase(60f, "01:00")]
        [TestCase(3725f, "62:05")]
        [TestCase(7230f, "120:30")]
        public void Show_WithADuration_FormatsMinutesAndSeconds(float seconds, string expected)
        {
            _views.GameOver.Show(Summary(duration: seconds));

            Assert.That(UiTestViews.Shown(_views.OverTime), Is.EqualTo(expected));
        }

        /// <summary>
        /// The score rolls from 0 on the fake clock, shows an in-between value half way and ends on the exact score
        /// after 0.3 s.
        /// </summary>
        [UnityTest]
        public IEnumerator Show_ScoreCountUp_EndsOnTheExactScoreAfterTheDuration()
        {
            _views.GameOver.Show(Summary(score: 1000));
            Assert.That(UiTestViews.Shown(_views.OverScore), Is.EqualTo("0"));

            yield return Advance(UiAnimation.COUNT_UP_SECONDS / 2d);
            var half = int.Parse(UiTestViews.Shown(_views.OverScore));
            Assert.That(half, Is.GreaterThan(0).And.LessThan(1000));

            yield return Advance(UiAnimation.COUNT_UP_SECONDS);
            Assert.That(UiTestViews.Shown(_views.OverScore), Is.EqualTo("1000"));
        }

        /// <summary>
        /// A click on Retry after the lock raises the event exactly once and starts nothing else.
        /// </summary>
        [UnityTest]
        public IEnumerator Retry_WhenClickedAfterTheLock_RaisesRetryClickedOnce()
        {
            var count = 0;
            _views.GameOver.RetryClicked += () => count++;
            _views.GameOver.Show(Summary());
            yield return Advance(UiAnimation.RETRY_LOCK_SECONDS);

            _views.OverRetry.onClick.Invoke();

            Assert.That(count, Is.EqualTo(1));
        }

        /// <summary>
        /// A tap before the Retry lock ends is ignored, and the button is not interactable meanwhile.
        /// </summary>
        [UnityTest]
        public IEnumerator Retry_WhenClickedBeforeTheLockEnds_RaisesNothing()
        {
            var count = 0;
            _views.GameOver.RetryClicked += () => count++;
            _views.GameOver.Show(Summary());
            yield return Advance(UiAnimation.RETRY_LOCK_SECONDS / 2d);

            _views.OverRetry.onClick.Invoke();

            Assert.That(count, Is.EqualTo(0));
            Assert.That(_views.OverRetry.interactable, Is.False);
        }

        /// <summary>
        /// A double tap raises Retry exactly once.
        /// </summary>
        [UnityTest]
        public IEnumerator Retry_WhenTappedTwice_RaisesRetryClickedOnce()
        {
            var count = 0;
            _views.GameOver.RetryClicked += () => count++;
            _views.GameOver.Show(Summary());
            yield return Advance(UiAnimation.RETRY_LOCK_SECONDS);

            _views.OverRetry.onClick.Invoke();
            _views.OverRetry.onClick.Invoke();

            Assert.That(count, Is.EqualTo(1));
        }

        /// <summary>
        /// Showing the view again after a Retry accepts one new Retry.
        /// </summary>
        [UnityTest]
        public IEnumerator Retry_AfterShowingAgain_IsAcceptedOnceMore()
        {
            var count = 0;
            _views.GameOver.RetryClicked += () => count++;
            _views.GameOver.Show(Summary());
            yield return Advance(UiAnimation.RETRY_LOCK_SECONDS);
            _views.OverRetry.onClick.Invoke();

            _views.GameOver.Show(Summary());
            yield return Advance(UiAnimation.RETRY_LOCK_SECONDS);
            _views.OverRetry.onClick.Invoke();

            Assert.That(count, Is.EqualTo(2));
        }

        /// <summary>
        /// After Hide the view no longer listens to the Retry button.
        /// </summary>
        [Test]
        public void Retry_AfterHide_RaisesNothing()
        {
            var count = 0;
            _views.GameOver.RetryClicked += () => count++;
            _views.GameOver.Show(Summary());
            _views.GameOver.Hide();

            _views.OverRetry.onClick.Invoke();

            Assert.That(count, Is.EqualTo(0));
        }

        /// <summary>
        /// The presenter forwards the Retry click to its own event.
        /// </summary>
        [UnityTest]
        public IEnumerator Presenter_WhenRetryIsClicked_RaisesRetryRequested()
        {
            var presenter = new GameOverPresenter(_views.GameOver, tier => null);
            var count = 0;
            presenter.RetryRequested += () => count++;
            presenter.Present(Summary());
            yield return Advance(UiAnimation.RETRY_LOCK_SECONDS);

            _views.OverRetry.onClick.Invoke();
            presenter.Dispose();

            Assert.That(count, Is.EqualTo(1));
        }

        /// <summary>
        /// The presenter gives the view the sprite of the highest tier reached.
        /// </summary>
        [Test]
        public void Presenter_Present_SetsTheHighestTierIcon()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            var asked = -1;
            var presenter = new GameOverPresenter(_views.GameOver, tier =>
            {
                asked = tier;
                return sprite;
            });

            presenter.Present(Summary(highestTier: 7));

            Assert.That(asked, Is.EqualTo(7));
            Assert.That(_views.OverIcon.sprite, Is.SameAs(sprite));
            presenter.Dispose();
            Object.Destroy(sprite);
            Object.Destroy(texture);
        }

        /// <summary>
        /// Menu is a disabled placeholder in M1.
        /// </summary>
        [Test]
        public void Menu_InM1_IsNotInteractable()
        {
            _views.GameOver.Show(Summary());

            Assert.That(_views.OverMenu.interactable, Is.False);
        }

        /// <summary>
        /// Hide removes the view from the screen.
        /// </summary>
        [Test]
        public void Hide_AfterShow_HidesTheView()
        {
            _views.GameOver.Show(Summary());

            _views.GameOver.Hide();

            Assert.That(_views.GameOverObject.activeSelf, Is.False);
        }

        /// <summary>
        /// The view starts transparent and is opaque after the fade, even when the game is frozen with a time
        /// scale of 0, because the fade uses unscaled time (S-64).
        /// </summary>
        [UnityTest]
        public IEnumerator Show_WithTimeScaleZero_FadesInOnUnscaledTime()
        {
            Time.timeScale = 0f;

            _views.GameOver.Show(Summary());
            Assert.That(_views.OverGroup.alpha, Is.EqualTo(0f));

            yield return new WaitForSecondsRealtime(GameOverView.FADE_SECONDS + 0.2f);

            Assert.That(_views.OverGroup.alpha, Is.EqualTo(1f));
        }
    }
}
