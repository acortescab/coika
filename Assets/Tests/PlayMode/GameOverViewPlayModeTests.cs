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
    /// fade-in runs on unscaled time. The <see cref="GameOverPresenter"/> forwards the click.
    /// </summary>
    public class GameOverViewPlayModeTests
    {
        private UiTestViews _views;

        /// <summary>
        /// Builds the views; the Game Over object starts inactive, as in the prefab.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _views = new UiTestViews();
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
        /// Show activates the view and writes the score, the best score, the pieces and the time.
        /// </summary>
        [Test]
        public void Show_WithASummary_ShowsItsValues()
        {
            _views.GameOver.Show(new RunSummary(4321, 5000, false, 6, 87, 40, 5, 125.5f));

            Assert.That(_views.GameOver.IsShown, Is.True);
            Assert.That(UiTestViews.Shown(_views.OverScore), Is.EqualTo("4321"));
            Assert.That(UiTestViews.Shown(_views.OverBest), Is.EqualTo("5000"));
            Assert.That(UiTestViews.Shown(_views.OverPieces), Is.EqualTo("87"));
            Assert.That(UiTestViews.Shown(_views.OverTime), Is.EqualTo("2:05"));
        }

        /// <summary>
        /// The banner is visible for a new best score.
        /// </summary>
        [Test]
        public void Show_WithANewBest_ShowsTheBanner()
        {
            _views.GameOver.Show(new RunSummary(900, 900, true, 3, 10, 5, 2, 30f));

            Assert.That(_views.OverNewBest.activeSelf, Is.True);
        }

        /// <summary>
        /// The banner is hidden when the run did not beat the best, even after a previous new best.
        /// </summary>
        [Test]
        public void Show_WithoutANewBest_HidesTheBanner()
        {
            _views.GameOver.Show(new RunSummary(900, 900, true, 3, 10, 5, 2, 30f));

            _views.GameOver.Show(new RunSummary(100, 900, false, 1, 4, 1, 1, 10f));

            Assert.That(_views.OverNewBest.activeSelf, Is.False);
        }

        /// <summary>
        /// Minutes and seconds roll over correctly, with the seconds padded to two digits.
        /// </summary>
        [TestCase(0f, "0:00")]
        [TestCase(59.9f, "0:59")]
        [TestCase(60f, "1:00")]
        [TestCase(3725f, "62:05")]
        public void Show_WithADuration_FormatsMinutesAndSeconds(float seconds, string expected)
        {
            _views.GameOver.Show(new RunSummary(1, 1, false, 0, 1, 0, 0, seconds));

            Assert.That(UiTestViews.Shown(_views.OverTime), Is.EqualTo(expected));
        }

        /// <summary>
        /// A click on Retry raises the event exactly once and starts nothing else.
        /// </summary>
        [Test]
        public void Retry_WhenClicked_RaisesRetryClickedOnce()
        {
            var count = 0;
            _views.GameOver.RetryClicked += () => count++;
            _views.GameOver.Show(new RunSummary(1, 1, false, 0, 1, 0, 0, 1f));

            _views.OverRetry.onClick.Invoke();

            Assert.That(count, Is.EqualTo(1));
        }

        /// <summary>
        /// The presenter forwards the Retry click to its own event.
        /// </summary>
        [Test]
        public void Presenter_WhenRetryIsClicked_RaisesRetryRequested()
        {
            var presenter = new GameOverPresenter(_views.GameOver, tier => null);
            var count = 0;
            presenter.RetryRequested += () => count++;
            presenter.Present(new RunSummary(1, 1, false, 0, 1, 0, 0, 1f));

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

            presenter.Present(new RunSummary(1, 1, false, 7, 1, 0, 0, 1f));

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
            _views.GameOver.Show(new RunSummary(1, 1, false, 0, 1, 0, 0, 1f));

            Assert.That(_views.OverMenu.interactable, Is.False);
        }

        /// <summary>
        /// Hide removes the view from the screen.
        /// </summary>
        [Test]
        public void Hide_AfterShow_HidesTheView()
        {
            _views.GameOver.Show(new RunSummary(1, 1, false, 0, 1, 0, 0, 1f));

            _views.GameOver.Hide();

            Assert.That(_views.GameOver.IsShown, Is.False);
        }

        /// <summary>
        /// The view starts transparent and is opaque after the fade, even when the game is frozen with a time
        /// scale of 0, because the fade uses unscaled time (S-64).
        /// </summary>
        [UnityTest]
        public IEnumerator Show_WithTimeScaleZero_FadesInOnUnscaledTime()
        {
            Time.timeScale = 0f;

            _views.GameOver.Show(new RunSummary(1, 1, false, 0, 1, 0, 0, 1f));
            Assert.That(_views.OverGroup.alpha, Is.EqualTo(0f));

            yield return new WaitForSecondsRealtime(GameOverView.FADE_SECONDS + 0.2f);

            Assert.That(_views.OverGroup.alpha, Is.EqualTo(1f));
        }
    }
}
