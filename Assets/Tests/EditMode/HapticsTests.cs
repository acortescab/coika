using System;
using System.IO;
using Coika.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="Haptics"/> (issue #30): the setting, the 50 ms throttle, "heavier wins", the self-disable
    /// after a backend failure, no allocations, and that the Android manifest asks for VIBRATE and no network.
    /// </summary>
    public class HapticsTests
    {
        private FakeHapticsBackend _backend;
        private SettingsService _settings;
        private Haptics _haptics;
        private double _now;

        /// <summary>
        /// Builds the wrapper over a fake backend, a fake clock and default settings.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _backend = new FakeHapticsBackend();
            _settings = new SettingsService(new SaveSystem(new FakeSaveStorage()));
            _now = 10.0;
            _haptics = new Haptics(_backend, _settings, () => _now);
        }

        /// <summary>
        /// With the setting on, an effect reaches the backend.
        /// </summary>
        [Test]
        public void Play_SettingOn_CallsBackend()
        {
            _haptics.Play(HapticKind.Medium);

            Assert.That(_backend.Played, Is.EqualTo(new[] { HapticKind.Medium }));
        }

        /// <summary>
        /// With the setting off, the backend is never called, whatever the kind.
        /// </summary>
        [Test]
        public void Play_SettingOff_MakesNoNativeCall()
        {
            _settings.Haptics = false;

            foreach (HapticKind kind in Enum.GetValues(typeof(HapticKind)))
            {
                _now += 1.0;
                _haptics.Play(kind);
            }

            Assert.That(_backend.Calls, Is.EqualTo(0));
        }

        /// <summary>
        /// Turning the setting back on takes effect at once.
        /// </summary>
        [Test]
        public void Play_SettingTurnedBackOn_CallsBackend()
        {
            _settings.Haptics = false;
            _haptics.Play(HapticKind.Light);
            _settings.Haptics = true;
            _haptics.Play(HapticKind.Light);

            Assert.That(_backend.Calls, Is.EqualTo(1));
        }

        /// <summary>
        /// Without a settings service the haptics stay on.
        /// </summary>
        [Test]
        public void Play_NullSettings_CallsBackend()
        {
            var haptics = new Haptics(_backend, null, () => _now);

            haptics.Play(HapticKind.Light);

            Assert.That(_backend.Calls, Is.EqualTo(1));
        }

        /// <summary>
        /// A second haptic of the same kind inside 50 ms is dropped.
        /// </summary>
        [Test]
        public void Play_SameKindWithinWindow_IsDropped()
        {
            _haptics.Play(HapticKind.Medium);
            _now += 0.049;
            _haptics.Play(HapticKind.Medium);

            Assert.That(_backend.Calls, Is.EqualTo(1));
        }

        /// <summary>
        /// A lighter haptic inside the window is dropped.
        /// </summary>
        [Test]
        public void Play_LighterWithinWindow_IsDropped()
        {
            _haptics.Play(HapticKind.Heavy);
            _now += 0.01;
            _haptics.Play(HapticKind.Medium);

            Assert.That(_backend.Played, Is.EqualTo(new[] { HapticKind.Heavy }));
        }

        /// <summary>
        /// A heavier haptic inside the window wins and plays.
        /// </summary>
        [Test]
        public void Play_HeavierWithinWindow_Wins()
        {
            _haptics.Play(HapticKind.Light);
            _now += 0.01;
            _haptics.Play(HapticKind.Heavy);

            Assert.That(_backend.Played, Is.EqualTo(new[] { HapticKind.Light, HapticKind.Heavy }));
        }

        /// <summary>
        /// The strength order is VeryLight, Light, Medium, Heavy, Long, not the declaration order of the enum.
        /// </summary>
        [Test]
        public void Play_LightAfterVeryLightWithinWindow_Wins()
        {
            _haptics.Play(HapticKind.VeryLight);
            _now += 0.01;
            _haptics.Play(HapticKind.Light);

            Assert.That(_backend.Played, Is.EqualTo(new[] { HapticKind.VeryLight, HapticKind.Light }));
        }

        /// <summary>
        /// Once the window has passed, any kind plays again.
        /// </summary>
        [Test]
        public void Play_AfterWindow_PlaysAgain()
        {
            _haptics.Play(HapticKind.Heavy);
            _now += Haptics.MIN_INTERVAL;
            _haptics.Play(HapticKind.VeryLight);

            Assert.That(_backend.Played, Is.EqualTo(new[] { HapticKind.Heavy, HapticKind.VeryLight }));
        }

        /// <summary>
        /// A dropped haptic does not extend the window.
        /// </summary>
        [Test]
        public void Play_DroppedCall_DoesNotRestartWindow()
        {
            _haptics.Play(HapticKind.Medium);
            _now += 0.04;
            _haptics.Play(HapticKind.Light);
            _now += 0.02;
            _haptics.Play(HapticKind.Light);

            Assert.That(_backend.Played, Is.EqualTo(new[] { HapticKind.Medium, HapticKind.Light }));
        }

        /// <summary>
        /// A failing backend logs one warning, then the wrapper makes no more native calls.
        /// </summary>
        [Test]
        public void Play_BackendThrows_WarnsOnceAndDisables()
        {
            _backend.Throws = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Haptics disabled"));

            _haptics.Play(HapticKind.Medium);
            _now += 1.0;
            _haptics.Play(HapticKind.Heavy);

            Assert.That(_haptics.IsDisabled, Is.True);
            Assert.That(_backend.Calls, Is.EqualTo(1));
        }

        /// <summary>
        /// The constructor refuses a missing backend or clock.
        /// </summary>
        [Test]
        public void Constructor_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new Haptics(null, _settings, () => 0.0));
            Assert.Throws<ArgumentNullException>(() => new Haptics(_backend, _settings, null));
        }

        /// <summary>
        /// The null backend, used in the Editor, ignores every kind without an exception.
        /// </summary>
        [Test]
        public void NullBackend_AnyKind_DoesNothing()
        {
            var haptics = new Haptics(new NullHapticsBackend(), _settings, () => _now);

            foreach (HapticKind kind in Enum.GetValues(typeof(HapticKind)))
            {
                _now += 1.0;
                Assert.DoesNotThrow(() => haptics.Play(kind));
            }

            Assert.That(haptics.IsDisabled, Is.False);
        }

        /// <summary>
        /// Once warmed up, Play allocates nothing, played or throttled.
        /// </summary>
        [Test]
        public void Play_AfterWarmUp_AllocatesNothing()
        {
            _haptics.Play(HapticKind.Light);
            _now += 1.0;
            var kinds = new[] { HapticKind.VeryLight, HapticKind.Light, HapticKind.Medium, HapticKind.Heavy, HapticKind.Long };
            _backend.Played.Clear();

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    _now += 0.03;
                    _haptics.Play(kinds[i % kinds.Length]);
                }
            });

            Assert.That(allocations, Is.LessThanOrEqualTo(AllocationMeter.TOLERANCE_COUNT));
        }

        /// <summary>
        /// The Android manifests ask for VIBRATE and never for a network permission (GDD §17).
        /// </summary>
        [Test]
        public void AndroidManifests_Haptics_VibrateWithoutNetwork()
        {
            var root = Path.Combine(Application.dataPath, "Plugins", "Android");
            var manifests = Directory.GetFiles(root, "AndroidManifest.xml", SearchOption.AllDirectories);
            var text = string.Concat(Array.ConvertAll(manifests, File.ReadAllText));

            Assert.That(text, Does.Contain("android.permission.VIBRATE"));
            Assert.That(text, Does.Not.Contain("android.permission.INTERNET"));
            Assert.That(text, Does.Not.Contain("android.permission.ACCESS_NETWORK_STATE"));
        }
    }
}
