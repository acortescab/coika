using System;
using System.Threading.Tasks;
using Coika.Core;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests the per-scene clip bank (issue #29): it matches clips to sounds by name and releases what it loaded.
    /// </summary>
    public class SoundBankTests
    {
        private FakeAssetService _assets;
        private string[] _names;
        private int _next;

        /// <summary>
        /// Creates an asset service double that hands out audio clips with the names of the test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _next = 0;
            _names = new[] { "Land3", "Land1", "Land2", "Merge", "Gameplay", "Unknown" };
            _assets = new FakeAssetService
            {
                Provider = type => AudioClip.Create(_names[_next++], 100, 1, 22050, false)
            };
        }

        /// <summary>
        /// Destroys the clips.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _assets.Cleanup();
        }

        /// <summary>
        /// Variants are matched by the name without its number and come back in name order.
        /// </summary>
        [Test]
        public async Task LoadAsync_WithNumberedClips_GroupsThemAsVariantsInNameOrder()
        {
            var bank = new SoundBank(_assets, "a", "b", "c");
            await bank.LoadAsync();

            Assert.IsTrue(bank.TryGetSfx(SfxId.Land, out var variants));
            CollectionAssert.AreEqual(new[] { "Land1", "Land2", "Land3" }, Array.ConvertAll(variants, clip => clip.name));
        }

        /// <summary>
        /// A music clip is found by its track, and a sound the bank does not hold is not found.
        /// </summary>
        [Test]
        public async Task LoadAsync_WithAMusicClip_FindsTheTrackAndNotOtherSounds()
        {
            _names = new[] { "Gameplay" };
            var bank = new SoundBank(_assets, "music");
            await bank.LoadAsync();

            Assert.IsTrue(bank.TryGetMusic(MusicId.Gameplay, out _));
            Assert.IsFalse(bank.TryGetMusic(MusicId.Menu, out _));
            Assert.IsFalse(bank.TryGetSfx(SfxId.Merge, out _));
        }

        /// <summary>
        /// A clip whose name matches nothing is ignored with a warning.
        /// </summary>
        [Test]
        public async Task LoadAsync_WithAnUnknownClip_WarnsAndIgnoresIt()
        {
            _names = new[] { "Unknown" };
            var bank = new SoundBank(_assets, "x");

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "Audio clip 'Unknown' matches no SfxId or MusicId.");
            await bank.LoadAsync();

            Assert.IsFalse(bank.TryGetSfx(SfxId.Spawn, out _));
        }

        /// <summary>
        /// Disposing releases every load, once, and the bank forgets its clips.
        /// </summary>
        [Test]
        public async Task Dispose_AfterLoading_ReleasesEverythingOnce()
        {
            var bank = new SoundBank(_assets, "a", "b");
            _names = new[] { "Merge", "Gameplay" };
            await bank.LoadAsync();
            Assert.AreEqual(2, _assets.OutstandingHandles);

            bank.Dispose();
            bank.Dispose();

            Assert.AreEqual(0, _assets.OutstandingHandles);
            Assert.IsFalse(bank.TryGetSfx(SfxId.Merge, out _));
        }

        /// <summary>
        /// The bank knows which clips it loaded.
        /// </summary>
        [Test]
        public async Task Contains_ForItsOwnClipOnly_IsTrue()
        {
            _names = new[] { "Merge" };
            var bank = new SoundBank(_assets, "a");
            await bank.LoadAsync();
            bank.TryGetSfx(SfxId.Merge, out var variants);
            var other = AudioClip.Create("Other", 100, 1, 22050, false);

            Assert.IsTrue(bank.Contains(variants[0]));
            Assert.IsFalse(bank.Contains(other));
            UnityEngine.Object.DestroyImmediate(other);
        }

        /// <summary>
        /// A bank disposed while a load is pending releases what arrives and reports it.
        /// </summary>
        [Test]
        public void LoadAsync_DisposedWhilePending_ReleasesTheArrivingClips()
        {
            var gate = new TaskCompletionSource<bool>();
            _assets.Gate = gate.Task;
            _names = new[] { "Merge" };
            var bank = new SoundBank(_assets, "a");

            var load = bank.LoadAsync();
            bank.Dispose();
            gate.SetResult(true);

            Assert.ThrowsAsync<ObjectDisposedException>(async () => await load);
            Assert.AreEqual(0, _assets.OutstandingHandles);
        }

        /// <summary>
        /// A bank needs a service.
        /// </summary>
        [Test]
        public void Constructor_WithoutAService_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SoundBank(null, "a"));
        }
    }
}
