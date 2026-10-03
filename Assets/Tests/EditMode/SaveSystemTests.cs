using System.IO;
using Coika.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="SaveSystem"/>, <see cref="SaveData"/> and <see cref="FileSaveStorage"/> (issue #26):
    /// round trip, recovery from bad files, atomic writes and debounced saves.
    /// </summary>
    public class SaveSystemTests
    {
        private string _directory;
        private FakeSaveStorage _fake;

        /// <summary>
        /// Creates a fresh temp folder and fake storage.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "coika-save-" + System.Guid.NewGuid().ToString("N"));
            _fake = new FakeSaveStorage();
        }

        /// <summary>
        /// Deletes the temp folder.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// A saved file loads back as an equal save.
        /// </summary>
        [Test]
        public void SaveLoad_RoundTrip_ReturnsEqualData()
        {
            var system = new SaveSystem(new FileSaveStorage(_directory));
            system.Data.RecordRun(1234, 4, 20, 61.5f);
            system.Data.settings.music = 0.3f;
            system.Data.settings.language = "es";
            system.Save();

            var loaded = new SaveSystem(new FileSaveStorage(_directory));
            loaded.Load();

            Assert.That(loaded.Data.ContentEquals(system.Data), Is.True);
            Assert.That(loaded.Data.bestScore.classic, Is.EqualTo(1234));
        }

        /// <summary>
        /// A first launch has no file and gives defaults without a warning or a backup.
        /// </summary>
        [Test]
        public void Load_NoFile_GivesDefaults()
        {
            var system = new SaveSystem(_fake);

            system.Load();

            Assert.That(system.Data.ContentEquals(SaveData.CreateDefaults()), Is.True);
            Assert.That(_fake.BackupContent, Is.Null);
        }

        /// <summary>
        /// Corrupt, empty, truncated and unknown-version files each give defaults and a backup, without throwing.
        /// </summary>
        /// <param name="content">The bad file content.</param>
        [TestCase("{ this is not json")]
        [TestCase("")]
        [TestCase("{\"version\":1,\"bestScore\":{\"classic\":50,\"da")]
        [TestCase("{\"version\":99,\"highestTier\":5}")]
        public void Load_BadFile_GivesDefaultsAndBackup(string content)
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Save was reset to defaults"));
            _fake.Content = content;
            var system = new SaveSystem(_fake);

            Assert.DoesNotThrow(system.Load);

            Assert.That(system.Data.ContentEquals(SaveData.CreateDefaults()), Is.True);
            Assert.That(_fake.BackupContent, Is.EqualTo(content));
        }

        /// <summary>
        /// The file storage copies a bad file to save.bak.
        /// </summary>
        [Test]
        public void Load_CorruptFileOnDisk_CreatesSaveBak()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Save was reset to defaults"));
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, "save.json"), "garbage");
            var system = new SaveSystem(new FileSaveStorage(_directory));

            system.Load();

            Assert.That(File.ReadAllText(Path.Combine(_directory, "save.bak")), Is.EqualTo("garbage"));
        }

        /// <summary>
        /// A failure between the temp write and the replace leaves the previous save intact.
        /// </summary>
        [Test]
        public void Save_ReplaceFails_KeepsPreviousFile()
        {
            var system = new SaveSystem(_fake);
            system.Data.RecordRun(100, 1, 1, 1f);
            system.Save();
            var previous = _fake.Content;
            system.Data.RecordRun(999, 2, 1, 1f);
            _fake.FailReplace = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not be written"));

            var written = system.Save();

            Assert.That(written, Is.False);
            Assert.That(_fake.Content, Is.EqualTo(previous));
        }

        /// <summary>
        /// Several requests in the same frame cost one write.
        /// </summary>
        [Test]
        public void RequestSave_ManyRequests_WritesOnceOnFlush()
        {
            var system = new SaveSystem(_fake);

            system.RequestSave();
            system.RequestSave();
            system.RequestSave();
            system.FlushIfDirty();
            system.FlushIfDirty();

            Assert.That(_fake.WriteCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Folding a run raises the best score only when beaten and accumulates the totals.
        /// </summary>
        [Test]
        public void RecordRun_TwoRuns_KeepsBestAndAddsTotals()
        {
            var data = SaveData.CreateDefaults();

            data.RecordRun(500, 3, 10, 30f);
            data.RecordRun(200, 2, 5, 15f);

            Assert.That(data.bestScore.classic, Is.EqualTo(500));
            Assert.That(data.highestTier, Is.EqualTo(3));
            Assert.That(data.totals.games, Is.EqualTo(2));
            Assert.That(data.totals.merges, Is.EqualTo(15));
            Assert.That(data.totals.playSeconds, Is.EqualTo(45f));
            Assert.That(data.discoveredTiers[3], Is.True);
            Assert.That(data.discoveredTiers[4], Is.False);
        }
    }
}
