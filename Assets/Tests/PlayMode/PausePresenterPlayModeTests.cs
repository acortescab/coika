using Coika.Core;
using Coika.UI;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the pause flow (issue #35): the <see cref="PausePresenter"/> opens the <see cref="PauseView"/>, puts the
    /// reusable <see cref="ConfirmView"/> over it for Restart and Menu, handles the Back button, and plays the UI
    /// sounds. The views are built in code and the audio is a fake, so no scene or sound is needed.
    /// </summary>
    public class PausePresenterPlayModeTests
    {
        private UiTestViews _views;
        private FakeAudioService _audio;
        private PausePresenter _presenter;
        private int _resume;
        private int _restart;
        private int _menu;
        private int _settings;

        /// <summary>
        /// Builds the views and a presenter that counts what it asks for.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _resume = 0;
            _restart = 0;
            _menu = 0;
            _settings = 0;
            _views = new UiTestViews();
            _audio = new FakeAudioService();
            _presenter = new PausePresenter(_views.Pause, _views.Confirm, _audio);
            _presenter.ResumeRequested += () => _resume++;
            _presenter.RestartConfirmed += () => _restart++;
            _presenter.MenuConfirmed += () => _menu++;
            _presenter.SettingsRequested += () => _settings++;
        }

        /// <summary>
        /// Disposes the presenter and destroys the views.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            _views.Destroy();
        }

        /// <summary>
        /// Open shows the pause menu, and a second Open changes nothing.
        /// </summary>
        [Test]
        public void Open_Twice_ShowsThePauseMenuOnce()
        {
            _presenter.Open();
            _presenter.Open();

            Assert.That(_views.PauseObject.activeSelf, Is.True);
            Assert.That(_presenter.IsOpen, Is.True);
            Assert.That(_presenter.IsConfirming, Is.False);
        }

        /// <summary>
        /// Close hides every panel, including a dialog that was open over the menu.
        /// </summary>
        [Test]
        public void Close_WithADialogOpen_HidesEverything()
        {
            _presenter.Open();
            _views.PauseRestart.onClick.Invoke();

            _presenter.Close();

            Assert.That(_views.PauseObject.activeSelf, Is.False);
            Assert.That(_views.ConfirmObject.activeSelf, Is.False);
            Assert.That(_presenter.IsOpen, Is.False);
        }

        /// <summary>
        /// Resume raises one request and plays the click; it does not change the state itself.
        /// </summary>
        [Test]
        public void Resume_WhenClicked_RaisesTheRequestAndClicks()
        {
            _presenter.Open();

            _views.PauseResume.onClick.Invoke();

            Assert.That(_resume, Is.EqualTo(1));
            Assert.That(_audio.SfxCalls.Count, Is.EqualTo(1));
            Assert.That(_audio.SfxCalls[0].Id, Is.EqualTo(SfxId.UiClick));
        }

        /// <summary>
        /// Restart asks first: the dialog opens over the menu and nothing restarts until it is confirmed.
        /// </summary>
        [Test]
        public void Restart_WhenClicked_AsksForConfirmationFirst()
        {
            _presenter.Open();

            _views.PauseRestart.onClick.Invoke();

            Assert.That(_views.ConfirmObject.activeSelf, Is.True);
            Assert.That(_views.PauseObject.activeSelf, Is.True);
            Assert.That(_presenter.IsConfirming, Is.True);
            Assert.That(_restart, Is.EqualTo(0));
        }

        /// <summary>
        /// Confirming the Restart dialog closes it and raises one RestartConfirmed.
        /// </summary>
        [Test]
        public void Restart_WhenConfirmed_RaisesRestartConfirmedOnce()
        {
            _presenter.Open();
            _views.PauseRestart.onClick.Invoke();

            _views.ConfirmYes.onClick.Invoke();

            Assert.That(_restart, Is.EqualTo(1));
            Assert.That(_menu, Is.EqualTo(0));
            Assert.That(_views.ConfirmObject.activeSelf, Is.False);
        }

        /// <summary>
        /// Cancelling a dialog closes it, raises nothing and leaves the menu open.
        /// </summary>
        [Test]
        public void Restart_WhenCancelled_ChangesNothing()
        {
            _presenter.Open();
            _views.PauseRestart.onClick.Invoke();

            _views.ConfirmCancel.onClick.Invoke();

            Assert.That(_restart, Is.EqualTo(0));
            Assert.That(_views.ConfirmObject.activeSelf, Is.False);
            Assert.That(_views.PauseObject.activeSelf, Is.True);
        }

        /// <summary>
        /// A cancelled dialog does not leave its action behind: the next dialog confirms its own.
        /// </summary>
        [Test]
        public void Confirm_AfterACancelledOtherDialog_RaisesOnlyItsOwnEvent()
        {
            _presenter.Open();
            _views.PauseRestart.onClick.Invoke();
            _views.ConfirmCancel.onClick.Invoke();

            // The Menu button is a disabled placeholder, so its click is raised on the view directly.
            _views.PauseMenu.onClick.Invoke();
            _views.ConfirmYes.onClick.Invoke();

            Assert.That(_restart, Is.EqualTo(0));
            Assert.That(_menu, Is.EqualTo(1));
        }

        /// <summary>
        /// Back with a dialog open cancels it, plays UiBack and keeps the menu.
        /// </summary>
        [Test]
        public void HandleBack_WithADialogOpen_CancelsIt()
        {
            _presenter.Open();
            _views.PauseRestart.onClick.Invoke();
            _audio.SfxCalls.Clear();

            var used = _presenter.HandleBack();

            Assert.That(used, Is.True);
            Assert.That(_views.ConfirmObject.activeSelf, Is.False);
            Assert.That(_views.PauseObject.activeSelf, Is.True);
            Assert.That(_restart, Is.EqualTo(0));
            Assert.That(_resume, Is.EqualTo(0));
            Assert.That(_audio.SfxCalls[0].Id, Is.EqualTo(SfxId.UiBack));
        }

        /// <summary>
        /// Back on the pause menu itself asks to resume.
        /// </summary>
        [Test]
        public void HandleBack_OnThePauseMenu_AsksToResume()
        {
            _presenter.Open();

            var used = _presenter.HandleBack();

            Assert.That(used, Is.True);
            Assert.That(_resume, Is.EqualTo(1));
        }

        /// <summary>
        /// Back with nothing open is not used, so the owner can open the menu instead.
        /// </summary>
        [Test]
        public void HandleBack_WhenClosed_IsNotUsed()
        {
            Assert.That(_presenter.HandleBack(), Is.False);
            Assert.That(_resume, Is.EqualTo(0));
        }

        /// <summary>
        /// Settings and Menu are disabled placeholders until #36 and M3.
        /// </summary>
        [Test]
        public void Open_Always_LeavesSettingsAndMenuDisabled()
        {
            _presenter.Open();

            Assert.That(_views.PauseSettings.interactable, Is.False);
            Assert.That(_views.PauseMenu.interactable, Is.False);
            Assert.That(_settings, Is.EqualTo(0));
        }

        /// <summary>
        /// Without an audio service the presenter works silently.
        /// </summary>
        [Test]
        public void Resume_WithoutAudio_StillRaisesTheRequest()
        {
            _presenter.Dispose();
            _presenter = new PausePresenter(_views.Pause, _views.Confirm, null);
            _presenter.ResumeRequested += () => _resume++;
            _presenter.Open();

            _views.PauseResume.onClick.Invoke();

            Assert.That(_resume, Is.EqualTo(1));
        }
    }
}
