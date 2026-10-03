using System.Collections.Generic;
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
            Assert.That(_settings.Language, Is.EqualTo("en"));
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
