using System.Collections.Generic;
using Coika.Core;
using Coika.Fx;
using NUnit.Framework;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// The feedback table of issue #34 end to end (issue #40): one scripted run on the real physics, with the real
    /// director, the fake audio, haptics and particles and the real time scale owner, produces a combo, a heavy merge, a
    /// Supernova and a game over, and each of them plays exactly what the table says. The unit tests of the director
    /// check one event at a time; this one checks that the events of a whole run do not disturb each other.
    /// </summary>
    public class FeedbackEndToEndPlayModeTests : HarnessTestBase
    {
        private const float TOLERANCE = 0.0001f;
        private const int SEED = 1234;

        private SimulationWorld _world;
        private int _sfxMark;
        private int _hapticMark;

        /// <summary>
        /// Builds a world with every M2 system and the slow-mo applied, and starts the run.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _world = new SimulationWorld(new SimulationOptions { Seed = SEED, Particles = false, SlowMo = true });
            _world.StartRun();

            // Any score would beat a best of 0 and play the fanfare, which has its own test; and the overlapping pairs
            // would land on each other and add thuds that this table does not look at.
            _world.Score.BestScore = int.MaxValue;
            TestReflection.SetField(_world.Config.Feedback.Animations, "_landImpulseThreshold", float.MaxValue);
        }

        /// <summary>
        /// Gives the world back.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        /// <summary>
        /// A scripted run of a combo, a tier 8 merge, a Supernova and a game over plays the whole table, in order.
        /// </summary>
        [Test]
        public void Run_WithAComboAHeavyMergeASupernovaAndAGameOver_PlaysTheWholeTable()
        {
            var feedback = _world.Config.Feedback;
            var heavyTier = feedback.ScreenFx.HeavyMergeMinTier;

            AssertCombo();
            AssertHeavyMerge(heavyTier);
            AssertSupernova();
            AssertGameOver();
        }

        /// <summary>
        /// Two merges inside the combo window: one pop each, the second one semitone higher, a Medium haptic each, a
        /// burst and a ring per merge, and no shake because the tier is light.
        /// </summary>
        private void AssertCombo()
        {
            MarkStart();
            MergeTwo(0);
            _world.Factory.ReleaseAll();
            MergeTwo(0);

            Assert.AreEqual(2, _world.Score.ComboTracker.Combo, "The second merge is in the combo window.");
            var sounds = NewSounds();
            Assert.AreEqual(2, sounds.Count);
            AssertSfx(sounds[0], SfxId.Merge, AudioMath.MergePitch(1, 0));
            AssertSfx(sounds[1], SfxId.Merge, AudioMath.MergePitch(1, 1));
            CollectionAssert.AreEqual(new[] { HapticKind.Medium, HapticKind.Medium }, NewHaptics());
            Assert.AreEqual(2, _world.FakeParticles.Count(FxKind.MergeBurst));
            Assert.AreEqual(2, _world.FakeParticles.Count(FxKind.FlashRing));
            Assert.IsEmpty(_world.ScreenFx.Shakes, "A light merge does not shake the screen.");
            Assert.AreEqual(0, _world.ScreenFx.SlowMos.Count);
        }

        /// <summary>
        /// A merge that creates the heavy tier: the pop and the deep layer, a Heavy haptic, a shake, and a slow-mo that
        /// reaches the time scale and ends by itself.
        /// </summary>
        /// <param name="heavyTier">Lowest tier that is heavy.</param>
        private void AssertHeavyMerge(int heavyTier)
        {
            _world.Factory.ReleaseAll();
            MarkStart();
            MergeTwo(heavyTier - 1);

            var sounds = NewSounds();
            Assert.AreEqual(2, sounds.Count);
            Assert.AreEqual(SfxId.Merge, sounds[0].Id);
            AssertSfx(sounds[1], SfxId.MergeBig, 1f);
            CollectionAssert.AreEqual(new[] { HapticKind.Heavy }, NewHaptics());
            Assert.AreEqual(1, _world.ScreenFx.Shakes.Count);
            Assert.AreEqual(1, _world.ScreenFx.SlowMos.Count);
            Assert.Less(_world.TimeScale.Value, 1f, "The slow-mo did not reach the time scale.");

            MergeScenario.RunOutSlowMo(_world);

            Assert.AreEqual(1f, _world.TimeScale.Value, "The slow-mo did not end.");
        }

        /// <summary>
        /// Two Black Holes: the boom, a Heavy haptic, the flash and the shockwave, a screen flash and a bigger shake, and
        /// no merge pop.
        /// </summary>
        private void AssertSupernova()
        {
            _world.Factory.ReleaseAll();
            MarkStart();
            var shakesBefore = _world.ScreenFx.Shakes.Count;
            MergeTwo(_world.Tiers.Count - 1, () => _world.Audio.SfxCalls.Exists(call => call.Id == SfxId.Supernova));

            var sounds = NewSounds();
            Assert.AreEqual(1, sounds.Count);
            AssertSfx(sounds[0], SfxId.Supernova, 1f);
            CollectionAssert.AreEqual(new[] { HapticKind.Heavy }, NewHaptics());
            Assert.AreEqual(1, _world.FakeParticles.Count(FxKind.SupernovaFlash));
            Assert.AreEqual(1, _world.FakeParticles.Count(FxKind.Shockwave));
            Assert.AreEqual(1, _world.ScreenFx.Flashes.Count);
            Assert.AreEqual(shakesBefore + 1, _world.ScreenFx.Shakes.Count);
            Assert.AreEqual(
                _world.Config.Feedback.ScreenFx.SupernovaShakeAmplitude,
                _world.ScreenFx.Shakes[_world.ScreenFx.Shakes.Count - 1].Amplitude,
                TOLERANCE);
        }

        /// <summary>
        /// The game over plays its sound and the long haptic once, and the run is over.
        /// </summary>
        private void AssertGameOver()
        {
            _world.Factory.ReleaseAll();
            MarkStart();
            _world.Manager.EndRun();

            var sounds = NewSounds();
            Assert.AreEqual(1, sounds.Count);
            AssertSfx(sounds[0], SfxId.GameOver, 1f);
            CollectionAssert.AreEqual(new[] { HapticKind.Long }, NewHaptics());
            Assert.IsTrue(_world.IsGameOver);
        }

        /// <summary>
        /// Remembers how many sounds and haptics were played, so a phase looks only at its own.
        /// </summary>
        private void MarkStart()
        {
            _sfxMark = _world.Audio.SfxCalls.Count;
            _hapticMark = _world.Haptics.Played.Count;
        }

        /// <summary>
        /// Gives the sounds played since <see cref="MarkStart"/>.
        /// </summary>
        /// <returns>The sounds, in order.</returns>
        private List<FakeAudioService.SfxCall> NewSounds()
        {
            return _world.Audio.SfxCalls.GetRange(_sfxMark, _world.Audio.SfxCalls.Count - _sfxMark);
        }

        /// <summary>
        /// Gives the haptics played since <see cref="MarkStart"/>.
        /// </summary>
        /// <returns>The haptics, in order.</returns>
        private List<HapticKind> NewHaptics()
        {
            return _world.Haptics.Played.GetRange(_hapticMark, _world.Haptics.Played.Count - _hapticMark);
        }

        /// <summary>
        /// Merges two pieces of a tier on the real physics.
        /// </summary>
        /// <param name="tier">Tier of the two pieces.</param>
        /// <param name="alsoDone">An extra stop condition, such as a supernova.</param>
        private void MergeTwo(int tier, System.Func<bool> alsoDone = null)
        {
            MergeScenario.MergeTwo(_world, tier, alsoDone);
        }

        /// <summary>
        /// Asserts the id and pitch of a recorded sound, at full volume.
        /// </summary>
        /// <param name="call">The recorded call.</param>
        /// <param name="id">The expected sound.</param>
        /// <param name="pitch">The expected pitch.</param>
        private static void AssertSfx(FakeAudioService.SfxCall call, SfxId id, float pitch)
        {
            Assert.AreEqual(id, call.Id);
            Assert.AreEqual(pitch, call.Pitch, TOLERANCE, $"Pitch of {id}.");
            Assert.AreEqual(1f, call.Volume, TOLERANCE, $"Volume of {id}.");
        }
    }
}
