using System.Collections.Generic;
using System.IO;
using Coika.Core;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="SettingsService"/> (issue #26): defaults, clamping, one event and one save per change,
    /// and that resetting progress keeps the settings.
    /// </summary>
    public class SettingsServiceTests
    {
        private FakeSaveStorage _storage;
        private SaveSystem _save;
        private SettingsService _settings;
        private List<SettingsChanged> _events;

        /// <summary>
        /// Creates the service over a fake storage and records its events.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _storage = new FakeSaveStorage();
            _save = new SaveSystem(_storage);
            _settings = new SettingsService(_save);
            _events = new List<SettingsChanged>();
            _settings.Changed += _events.Add;
        }

        /// <summary>
        /// A volume written by key is the one of its property, with the same event.
        /// </summary>
        [TestCase(SettingKey.Master)]
        [TestCase(SettingKey.Music)]
        [TestCase(SettingKey.Sfx)]
        public void SetFloat_ByKey_WritesTheMatchingVolume(SettingKey key)
        {
            _settings.SetFloat(key, 0.25f);

            Assert.That(_settings.GetFloat(key), Is.EqualTo(0.25f));
            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_events[0].Key, Is.EqualTo(key));
        }

        /// <summary>
        /// A toggle written by key is the one of its property, with the same event.
        /// </summary>
        [TestCase(SettingKey.Haptics)]
        [TestCase(SettingKey.GuideLine)]
        [TestCase(SettingKey.ReduceShake)]
        [TestCase(SettingKey.LeftHanded)]
        [TestCase(SettingKey.FingerOffset)]
        public void SetBool_ByKey_WritesTheMatchingToggle(SettingKey key)
        {
            var flipped = !_settings.GetBool(key);

            _settings.SetBool(key, flipped);

            Assert.That(_settings.GetBool(key), Is.EqualTo(flipped));
            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_events[0].Key, Is.EqualTo(key));
        }

        /// <summary>
        /// A key of the wrong kind is rejected instead of ignored.
        /// </summary>
        [Test]
        public void GetFloat_WithAToggleKey_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _settings.GetFloat(SettingKey.Haptics));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _settings.SetBool(SettingKey.Music, true));
        }

        /// <summary>
        /// A new save carries the GDD defaults.
        /// </summary>
        [Test]
        public void Defaults_NewSave_MatchGdd()
        {
            Assert.That(_settings.Master, Is.EqualTo(1f));
            Assert.That(_settings.Music, Is.EqualTo(0.8f));
            Assert.That(_settings.Sfx, Is.EqualTo(1f));
            Assert.That(_settings.Haptics, Is.True);
            Assert.That(_settings.GuideLine, Is.True);
            Assert.That(_settings.ReduceShake, Is.False);
            Assert.That(_settings.LeftHanded, Is.False);
            Assert.That(_settings.FingerOffset, Is.False);
            Assert.That(_settings.Language, Is.EqualTo("en"));
        }

        /// <summary>
        /// Changing the finger offset raises one event with its key and flag, and saves it.
        /// </summary>
        [Test]
        public void FingerOffset_Change_RaisesOneEventAndPersists()
        {
            _settings.FingerOffset = true;
            _settings.FingerOffset = true;
            _save.FlushIfDirty();

            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_events[0].Key, Is.EqualTo(SettingKey.FingerOffset));
            Assert.That(_events[0].Flag, Is.True);
            Assert.That(_save.Data.settings.fingerOffset, Is.True);
            Assert.That(_storage.WriteCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Every setting that was changed and saved to a real file comes back the same from a new save system on the
        /// same folder, which is what a restart of the game does (issue #40).
        /// </summary>
        [Test]
        public void Settings_AfterARestart_AreTheSavedOnes()
        {
            var directory = Path.Combine(Path.GetTempPath(), "coika-settings-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var firstSave = new SaveSystem(new FileSaveStorage(directory));
                firstSave.Load();
                var first = new SettingsService(firstSave)
                {
                    Master = 0.25f,
                    Music = 0.5f,
                    Sfx = 0.75f,
                    Haptics = false,
                    GuideLine = false,
                    ReduceShake = true,
                    LeftHanded = true,
                    FingerOffset = true
                };
                firstSave.FlushIfDirty();

                var secondSave = new SaveSystem(new FileSaveStorage(directory));
                secondSave.Load();
                var restarted = new SettingsService(secondSave);

                Assert.That(restarted.Master, Is.EqualTo(first.Master));
                Assert.That(restarted.Music, Is.EqualTo(first.Music));
                Assert.That(restarted.Sfx, Is.EqualTo(first.Sfx));
                Assert.That(restarted.Haptics, Is.False);
                Assert.That(restarted.GuideLine, Is.False);
                Assert.That(restarted.ReduceShake, Is.True);
                Assert.That(restarted.LeftHanded, Is.True);
                Assert.That(restarted.FingerOffset, Is.True);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        /// <summary>
        /// A change raises exactly one event and one save.
        /// </summary>
        [Test]
        public void Set_Change_RaisesOneEventAndOneSave()
        {
            _settings.Haptics = false;
            _save.FlushIfDirty();
            _save.FlushIfDirty();

            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_events[0].Key, Is.EqualTo(SettingKey.Haptics));
            Assert.That(_events[0].Flag, Is.False);
            Assert.That(_storage.WriteCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Setting the current value raises nothing and saves nothing.
        /// </summary>
        [Test]
        public void Set_SameValue_RaisesNothing()
        {
            _settings.GuideLine = true;
            _settings.Music = 0.8f;
            _save.FlushIfDirty();

            Assert.That(_events, Is.Empty);
            Assert.That(_storage.WriteCount, Is.EqualTo(0));
        }

        /// <summary>
        /// Volumes clamp to 0 to 1.
        /// </summary>
        [Test]
        public void SetVolume_OutOfRange_Clamps()
        {
            _settings.Master = 3f;
            _settings.Music = 5f;
            _settings.Sfx = -2f;

            Assert.That(_settings.Master, Is.EqualTo(1f));
            Assert.That(_settings.Music, Is.EqualTo(1f));
            Assert.That(_settings.Sfx, Is.EqualTo(0f));
        }

        /// <summary>
        /// Resetting progress clears scores and totals and keeps the settings.
        /// </summary>
        [Test]
        public void ResetProgress_AfterRuns_KeepsSettings()
        {
            _settings.Music = 0.2f;
            _save.Data.RecordRun(900, 5, 12, 40f);

            _settings.ResetProgress();

            Assert.That(_save.Data.bestScore.classic, Is.EqualTo(0));
            Assert.That(_save.Data.totals.games, Is.EqualTo(0));
            Assert.That(_save.Data.discoveredTiers[5], Is.False);
            Assert.That(_save.Data.discoveredTiers[0], Is.True);
            Assert.That(_settings.Music, Is.EqualTo(0.2f));
        }
    }
}
