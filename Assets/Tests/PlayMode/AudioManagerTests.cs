using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Coika.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Tests the audio engine (issue #29): the pooled voices, the landing rate limit, the volumes that follow the
    /// settings, the music, and the banks that give it its clips. The clock is fake and no mixer is used.
    /// </summary>
    public class AudioManagerTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private GameObject _host;
        private AudioManager _audio;
        private TestAssetService _assets;
        private SettingsService _settings;
        private SoundBank _bank;
        private double _now;

        /// <summary>
        /// Builds a manager with a fake clock and a voice prefab double, and adds a bank with the test clips.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _now = 100.0;
            _assets = new TestAssetService(_created) { PrefabFactory = BuildVoicePrefab };
            _settings = new SettingsService(new SaveSystem(new TestSaveStorage()))
            {
                Master = 1f,
                Music = 1f,
                Sfx = 1f
            };
            _host = new GameObject("Audio");
            _audio = _host.AddComponent<AudioManager>();
            _audio.InitializeAsync(_assets, new AssetReference("voice"), null, _settings, () => _now).GetAwaiter().GetResult();

            _bank = new SoundBank(_assets, SoundBank.SFX_LABEL, SoundBank.MUSIC_GAMEPLAY_LABEL);
            _bank.LoadAsync().GetAwaiter().GetResult();
            _audio.AddBank(_bank);
        }

        /// <summary>
        /// Destroys the manager, the bank and the assets.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            AudioListener.pause = false;
            _audio.RemoveBank(_bank);
            _bank.Dispose();
            UnityEngine.Object.DestroyImmediate(_host);
            foreach (var created in _created)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        /// <summary>
        /// The pool is built with 8 voices, all free.
        /// </summary>
        [Test]
        public void InitializeAsync_Always_BuildsEightFreeVoices()
        {
            Assert.AreEqual(8, _audio.VoiceCount);
            Assert.AreEqual(8, _audio.PooledVoiceCount);
            Assert.AreEqual(0, _audio.ActiveVoiceCount);
        }

        /// <summary>
        /// A second initialization is a programming error.
        /// </summary>
        [Test]
        public void InitializeAsync_Twice_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _audio.InitializeAsync(_assets, new AssetReference("voice"), null, _settings, () => _now).GetAwaiter().GetResult());
        }

        /// <summary>
        /// Every sound plays without throwing.
        /// </summary>
        [Test]
        public void PlaySfx_EveryId_DoesNotThrow()
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                Assert.DoesNotThrow(() => _audio.PlaySfx(id), id.ToString());
                _now += 0.1;
            }
        }

        /// <summary>
        /// Twenty requests in a row never use more than 8 voices, never throw and never make the pool grow.
        /// </summary>
        [Test]
        public void PlaySfx_TwentyRapidRequests_NeverExceedEightVoices()
        {
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    _audio.PlaySfx(SfxId.Merge, 1f + 0.01f * i);
                    _now += 0.001;
                    Assert.LessOrEqual(_audio.ActiveVoiceCount, 8);
                }
            });

            Assert.AreEqual(8, _audio.ActiveVoiceCount);
            Assert.AreEqual(0, _audio.PooledVoiceCount);
            Assert.AreEqual(8, _host.GetComponentsInChildren<AudioVoice>(true).Length, "the pool must not grow");
        }

        /// <summary>
        /// When all voices are busy the oldest one is the one that is stolen.
        /// </summary>
        [Test]
        public void PlaySfx_WithAllVoicesBusy_StealsTheOldest()
        {
            for (int i = 0; i < 8; i++)
            {
                _now += 1.0;
                _audio.PlaySfx(SfxId.Merge, 1f);
            }

            var oldestBefore = double.MaxValue;
            foreach (var voice in _host.GetComponentsInChildren<AudioVoice>())
            {
                oldestBefore = Math.Min(oldestBefore, voice.StartedAt);
            }

            _now += 1.0;
            _audio.PlaySfx(SfxId.Drop, 1f);

            foreach (var voice in _host.GetComponentsInChildren<AudioVoice>())
            {
                Assert.AreNotEqual(oldestBefore, voice.StartedAt);
            }
        }

        /// <summary>
        /// A sound whose bank was removed is dropped silently.
        /// </summary>
        [Test]
        public void PlaySfx_WithoutABank_IsDroppedSilently()
        {
            _audio.RemoveBank(_bank);

            Assert.DoesNotThrow(() => _audio.PlaySfx(SfxId.Merge));
            Assert.AreEqual(0, _audio.ActiveVoiceCount);
        }

        /// <summary>
        /// Landing sounds inside 40 ms of the last one are dropped, so only one voice is taken.
        /// </summary>
        [Test]
        public void PlaySfx_LandInsideTheInterval_IsDropped()
        {
            _audio.PlaySfx(SfxId.Land);
            _now += 0.01;
            _audio.PlaySfx(SfxId.Land);

            Assert.AreEqual(1, _audio.ActiveVoiceCount);
        }

        /// <summary>
        /// Playing a sound after the warm-up allocates nothing.
        /// </summary>
        [Test]
        public void PlaySfx_AfterWarmUp_AllocatesNothing()
        {
            _audio.PlaySfx(SfxId.Merge);
            _audio.PlaySfx(SfxId.Drop);

            var allocations = AllocationMeter.Measure(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    _audio.PlaySfx(SfxId.Merge, 1.2f, 0.8f);
                }
            });

            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT);
        }

        /// <summary>
        /// The volumes start at the settings: 1.0 is 0 dB.
        /// </summary>
        [Test]
        public void Volumes_AtFullSettings_AreZeroDb()
        {
            Assert.AreEqual(0f, _audio.SfxDb, 0.001f);
            Assert.AreEqual(0f, _audio.MusicDb, 0.001f);
        }

        /// <summary>
        /// Changing a volume in the settings applies at once, and Master multiplies both groups.
        /// </summary>
        [Test]
        public void Volumes_WhenTheSettingsChange_FollowAtOnce()
        {
            _settings.Sfx = 0.5f;
            Assert.AreEqual(-6.02f, _audio.SfxDb, 0.05f);

            _settings.Sfx = 0f;
            Assert.AreEqual(-80f, _audio.SfxDb);

            _settings.Sfx = 1f;
            _settings.Master = 0.5f;
            Assert.AreEqual(-6.02f, _audio.SfxDb, 0.05f);
            Assert.AreEqual(-6.02f, _audio.MusicDb, 0.05f);
        }

        /// <summary>
        /// The gameplay loop starts and stops on request.
        /// </summary>
        [Test]
        public void PlayMusic_Gameplay_StartsTheLoopAndStopMusicEndsIt()
        {
            _audio.PlayMusic(MusicId.Gameplay);
            Assert.IsTrue(_audio.IsMusicPlaying);

            _audio.StopMusic();
            Assert.IsFalse(_audio.IsMusicPlaying);
        }

        /// <summary>
        /// A track no bank has is ignored.
        /// </summary>
        [Test]
        public void PlayMusic_WithATrackNoBankHas_DoesNothing()
        {
            _audio.PlayMusic(MusicId.Menu);

            Assert.IsFalse(_audio.IsMusicPlaying);
        }

        /// <summary>
        /// Pausing stops the music and resuming continues it.
        /// </summary>
        [Test]
        public void PauseMusic_ThenResume_PausesAndContinues()
        {
            _audio.PlayMusic(MusicId.Gameplay);

            _audio.PauseMusic();
            Assert.IsFalse(_audio.IsMusicPlaying);

            _audio.ResumeMusic();
            Assert.IsTrue(_audio.IsMusicPlaying);
        }

        /// <summary>
        /// A duck lowers the music dB without touching the effects, and restoring brings it back.
        /// </summary>
        [Test]
        public void DuckMusic_ThenRestore_LowersAndRestoresTheMusicOnly()
        {
            _audio.DuckMusic(-6f, 0f);
            Assert.AreEqual(-6f, _audio.MusicDb, 0.001f);
            Assert.AreEqual(0f, _audio.SfxDb, 0.001f);

            _audio.DuckMusic(0f, 0f);
            Assert.AreEqual(0f, _audio.MusicDb, 0.001f);
        }

        /// <summary>
        /// Removing a bank stops the sounds and the music that use its clips and gives the voices back to the pool.
        /// </summary>
        [Test]
        public void RemoveBank_WhilePlaying_StopsItsSoundsAndFreesTheVoices()
        {
            _audio.PlayMusic(MusicId.Gameplay);
            _audio.PlaySfx(SfxId.Merge);
            Assert.AreEqual(1, _audio.ActiveVoiceCount);

            _audio.RemoveBank(_bank);

            Assert.IsFalse(_audio.IsMusicPlaying);
            Assert.AreEqual(0, _audio.ActiveVoiceCount);
            Assert.AreEqual(8, _audio.PooledVoiceCount);
        }

        /// <summary>
        /// A voice whose sound ended goes back to the pool on the next frame.
        /// </summary>
        [UnityTest]
        public IEnumerator Update_AfterASoundEnds_ReclaimsTheVoice()
        {
            _audio.PlaySfx(SfxId.Merge);
            Assert.AreEqual(1, _audio.ActiveVoiceCount);

            foreach (var voice in _host.GetComponentsInChildren<AudioVoice>())
            {
                voice.Source.Stop();
            }

            yield return null;

            Assert.AreEqual(0, _audio.ActiveVoiceCount);
            Assert.AreEqual(8, _audio.PooledVoiceCount);
        }

        /// <summary>
        /// A duck with a duration ramps on real time through Update and ends at its target.
        /// </summary>
        [UnityTest]
        public IEnumerator Update_WithADuckInProgress_RampsToTheTarget()
        {
            _audio.DuckMusic(-6f, 0.1f);
            Assert.Greater(_audio.MusicDb, -6f, "the duck must not jump");

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.AreEqual(-6f, _audio.MusicDb, 0.001f);
        }

        /// <summary>
        /// The app pausing pauses the listener, and coming back lifts it.
        /// </summary>
        [Test]
        public void OnApplicationPause_PausesAndRestoresTheListener()
        {
            _host.SendMessage("OnApplicationPause", true);
            Assert.IsTrue(AudioListener.pause);

            _host.SendMessage("OnApplicationPause", false);
            Assert.IsFalse(AudioListener.pause);
        }

        /// <summary>
        /// Destroying the manager stops it from listening to the settings.
        /// </summary>
        [Test]
        public void Destroy_Always_UnsubscribesFromTheSettings()
        {
            Assert.AreEqual(1, SubscriberCount());

            UnityEngine.Object.DestroyImmediate(_host);

            Assert.AreEqual(0, SubscriberCount());
        }

        /// <summary>
        /// Destroying the manager while voices play does not throw.
        /// </summary>
        [Test]
        public void Destroy_WhilePlaying_DoesNotThrow()
        {
            _audio.PlaySfx(SfxId.Merge);

            Assert.DoesNotThrow(() => UnityEngine.Object.DestroyImmediate(_host));
            Assert.DoesNotThrow(() => _audio.RemoveBank(_bank));
        }

        /// <summary>
        /// Counts the listeners of the change event of the settings.
        /// </summary>
        /// <returns>Number of subscribers.</returns>
        private int SubscriberCount()
        {
            var field = typeof(SettingsService).GetField(nameof(SettingsService.Changed), BindingFlags.Instance | BindingFlags.NonPublic);
            var handler = (Delegate)field.GetValue(_settings);
            return handler == null ? 0 : handler.GetInvocationList().Length;
        }

        /// <summary>
        /// Builds the double of the voice prefab: an inactive object with a source and an <see cref="AudioVoice"/>.
        /// </summary>
        /// <returns>The prefab.</returns>
        private static GameObject BuildVoicePrefab()
        {
            var prefab = new GameObject("AudioVoicePrefab");
            prefab.SetActive(false);
            prefab.AddComponent<AudioSource>().playOnAwake = false;
            prefab.AddComponent<AudioVoice>();
            return prefab;
        }
    }
}
