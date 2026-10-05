using System.Linq;
using Coika.Core;
using Coika.UI;
using NUnit.Framework;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the Settings screen (issue #36): the <see cref="SettingsPresenter"/> shows the saved values when the
    /// <see cref="SettingsView"/> opens, writes each change to the <see cref="SettingsService"/> at once, and the
    /// values survive a restart. The views are built in code, the audio is a fake and the save is in memory.
    /// </summary>
    public class SettingsPresenterPlayModeTests
    {
        private UiTestViews _views;
        private FakeAudioService _audio;
        private TestSaveStorage _storage;
        private SaveSystem _save;
        private SettingsService _settings;
        private SettingsPresenter _presenter;

        /// <summary>
        /// Builds the view over a settings service on an in-memory save.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _views = new UiTestViews();
            _audio = new FakeAudioService();
            _storage = new TestSaveStorage();
            _save = new SaveSystem(_storage);
            _save.Load();
            _settings = new SettingsService(_save);
            _presenter = new SettingsPresenter(_views.Settings, _settings, _audio);
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
        /// Opening the screen shows the saved value of every control, without writing anything back.
        /// </summary>
        [Test]
        public void Open_WithSavedValues_ShowsThemWithoutWritingThem()
        {
            foreach (var key in SettingsRows.Sliders)
            {
                _settings.SetFloat(key, 0.25f);
            }

            foreach (var key in SettingsRows.Toggles)
            {
                _settings.SetBool(key, !_settings.GetBool(key));
            }

            var changes = 0;
            _settings.Changed += _ => changes++;

            _views.Settings.Open();

            foreach (var key in SettingsRows.Sliders)
            {
                Assert.That(_views.SettingsSliders[key].value, Is.EqualTo(0.25f).Within(0.0001f), key.ToString());
            }

            foreach (var key in SettingsRows.Toggles)
            {
                Assert.That(_views.SettingsToggles[key].isOn, Is.EqualTo(_settings.GetBool(key)), key.ToString());
            }

            Assert.That(changes, Is.EqualTo(0));
        }
        /// <summary>
        /// Dragging a volume slider writes the volume at once, with no Apply step.
        /// </summary>
        [TestCaseSource(typeof(SettingsRows), nameof(SettingsRows.Sliders))]
        public void Slider_WhenDragged_WritesTheVolume(SettingKey key)
        {
            _views.Settings.Open();

            _views.SettingsSliders[key].value = 0.3f;

            Assert.That(_settings.GetFloat(key), Is.EqualTo(0.3f).Within(0.0001f));
        }

        /// <summary>
        /// Switching a toggle writes the matching setting, and no other one.
        /// </summary>
        [TestCaseSource(typeof(SettingsRows), nameof(SettingsRows.Toggles))]
        public void Toggle_WhenSwitched_WritesOnlyItsSetting(SettingKey key)
        {
            _views.Settings.Open();
            var before = SettingsRows.Toggles.ToDictionary(other => other, other => _settings.GetBool(other));

            _views.SettingsToggles[key].isOn = !before[key];

            foreach (var other in SettingsRows.Toggles)
            {
                Assert.That(_settings.GetBool(other), Is.EqualTo(other == key ? !before[other] : before[other]), other.ToString());
            }
        }
        /// <summary>
        /// Releasing the SFX slider plays a sound so the player hears the new volume.
        /// </summary>
        [Test]
        public void SfxSlider_WhenReleased_PlaysAPreview()
        {
            _views.Settings.Open();

            _views.SettingsSliders[SettingKey.Sfx].GetComponent<SliderReleaseNotifier>().OnPointerUp(null);

            Assert.That(_audio.SfxCalls.Count, Is.EqualTo(1));
            Assert.That(_audio.SfxCalls[0].Id, Is.EqualTo(SfxId.UiClick));
        }

        /// <summary>
        /// The music is audible while it is dragged, so releasing its slider plays no preview.
        /// </summary>
        [Test]
        public void MusicSlider_WhenReleased_PlaysNothing()
        {
            _views.Settings.Open();

            _views.SettingsSliders[SettingKey.Music].GetComponent<SliderReleaseNotifier>().OnPointerUp(null);

            Assert.That(_audio.SfxCalls.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// Dragging a slider plays no preview: it plays on release only.
        /// </summary>
        [Test]
        public void SfxSlider_WhenDragged_PlaysNothing()
        {
            _views.Settings.Open();

            _views.SettingsSliders[SettingKey.Sfx].value = 0.1f;
            _views.SettingsSliders[SettingKey.Sfx].value = 0.2f;

            Assert.That(_audio.SfxCalls.Count, Is.EqualTo(0));
        }

        /// <summary>
        /// A toggle never shows its state only by colour: its text reads ON or OFF as it is switched.
        /// </summary>
        [Test]
        public void Toggle_WhenSwitched_UpdatesItsStateText()
        {
            _views.Settings.Open();
            var label = _views.SettingsToggleStates[SettingKey.Haptics];
            var offBefore = label.StringReference;

            _views.SettingsToggles[SettingKey.Haptics].isOn = !_settings.Haptics;

            Assert.That(label.StringReference, Is.Not.SameAs(offBefore));
            Assert.That(label.StringReference.TableEntryReference.Key, Is.EqualTo(_settings.Haptics ? UiTextKeys.SETTINGS_ON : UiTextKeys.SETTINGS_OFF));
        }

        /// <summary>
        /// What the player changed is still there after the app restarts: a new save over the same storage, a new
        /// service and a new screen show the changed values.
        /// </summary>
        [Test]
        public void Changes_AfterARestart_AreShownAgain()
        {
            _views.Settings.Open();
            _views.SettingsSliders[SettingKey.Music].value = 0.1f;
            _views.SettingsToggles[SettingKey.GuideLine].isOn = false;
            _views.SettingsToggles[SettingKey.LeftHanded].isOn = true;
            _save.Save();
            _presenter.Dispose();
            _views.Destroy();

            _views = new UiTestViews();
            var restartedSave = new SaveSystem(_storage);
            restartedSave.Load();
            _presenter = new SettingsPresenter(_views.Settings, new SettingsService(restartedSave), _audio);
            _views.Settings.Open();

            Assert.That(_views.SettingsSliders[SettingKey.Music].value, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(_views.SettingsToggles[SettingKey.GuideLine].isOn, Is.False);
            Assert.That(_views.SettingsToggles[SettingKey.LeftHanded].isOn, Is.True);
        }
    }
}
