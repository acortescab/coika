using System;
using Coika.Core;
using Coika.Data;
using Coika.Fx;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the sound and the haptic that <see cref="FeedbackDirector"/> maps to each gameplay event of the GDD §9
    /// table (issue #34): the sound ids, the pitch and volume rules, the haptic kinds and the tier thresholds. It uses
    /// the fake audio and haptics of the simulation harness and real merges, landings and run ends.
    /// </summary>
    public class FeedbackDirectorPlayModeTests : HarnessTestBase
    {
        private const float TOLERANCE = 0.0001f;
        private const float FALL_HEIGHT = 8f;
        private const double DANGER_SECONDS = 1.0;

        private SimulationWorld _world;
        private FeedbackConfig _feedback;

        /// <summary>
        /// Builds a world without real particles and starts a run, with the director bound to fake services.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _world = new SimulationWorld(new SimulationOptions { Seed = 1, Particles = false });
            _feedback = _world.Config.Feedback;
            _world.StartRun();

            // Any score would beat a best of 0 and play the fanfare, which only its own test looks at.
            _world.Score.BestScore = int.MaxValue;
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
        /// The next piece appearing after a drop plays the soft bloop and nothing else.
        /// </summary>
        [Test]
        public void Spawn_AfterADrop_PlaysTheSpawnSoundWithoutHaptic()
        {
            Raise(_world.Controller, "PieceSpawned", 0);

            Assert.AreEqual(1, _world.Audio.SfxCalls.Count);
            AssertSfx(_world.Audio.SfxCalls[0], SfxId.Spawn, 1f, 1f);
            Assert.IsEmpty(_world.Haptics.Played);
        }

        /// <summary>
        /// A drop plays the whoosh at the low pitch of the config and a light haptic.
        /// </summary>
        [Test]
        public void Drop_Always_PlaysALowWhooshAndALightHaptic()
        {
            Raise(_world.Controller, "PieceDropped", 3);

            Assert.AreEqual(1, _world.Audio.SfxCalls.Count);
            AssertSfx(_world.Audio.SfxCalls[0], SfxId.Drop, _feedback.Sound.DropPitch, 1f);
            Assert.Less(_feedback.Sound.DropPitch, 1f, "The whoosh is low.");
            CollectionAssert.AreEqual(new[] { HapticKind.Light }, _world.Haptics.Played);
        }

        /// <summary>
        /// A landing above the threshold plays the thud, louder with the impulse and higher for smaller pieces, with a
        /// very light haptic.
        /// </summary>
        [Test]
        public void Land_AboveTheThreshold_PlaysThudByImpulseAndSize()
        {
            var small = _world.Factory.Create(_world.Tiers[0], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
            var big = _world.Factory.Create(_world.Tiers[8], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);
            var soft = _feedback.Animations.LandImpulseThreshold + 1f;
            var hard = _feedback.Animations.LandImpulseThreshold + 5f;

            Raise(small, "Landed", small, soft);
            Raise(small, "Landed", small, hard);
            Raise(big, "Landed", big, hard);

            var calls = _world.Audio.SfxCalls;
            Assert.AreEqual(3, calls.Count);
            AssertSfx(calls[0], SfxId.Land, _feedback.Sound.LandPitch(_world.Tiers[0].Radius), _feedback.Sound.LandVolume(soft));
            AssertSfx(calls[1], SfxId.Land, _feedback.Sound.LandPitch(_world.Tiers[0].Radius), _feedback.Sound.LandVolume(hard));
            AssertSfx(calls[2], SfxId.Land, _feedback.Sound.LandPitch(_world.Tiers[8].Radius), _feedback.Sound.LandVolume(hard));
            Assert.Greater(calls[1].Volume, calls[0].Volume, "Volume grows with the impulse.");
            Assert.Greater(calls[1].Pitch, calls[2].Pitch, "Smaller pieces sound higher.");
            CollectionAssert.AreEqual(new[] { HapticKind.VeryLight, HapticKind.VeryLight, HapticKind.VeryLight }, _world.Haptics.Played);
        }

        /// <summary>
        /// A landing below the threshold makes no sound and no haptic.
        /// </summary>
        [Test]
        public void Land_BelowTheThreshold_PlaysNothing()
        {
            var piece = _world.Factory.Create(_world.Tiers[2], new Vector2(0f, _world.Jar.FloorY + FALL_HEIGHT), Vector2.zero);

            Raise(piece, "Landed", piece, _feedback.Animations.LandImpulseThreshold * 0.5f);

            Assert.IsEmpty(_world.Audio.SfxCalls);
            Assert.IsEmpty(_world.Haptics.Played);
        }

        /// <summary>
        /// A merge plays the pop pitched by the created tier, the deep layer from the heavy tier up, and a Medium
        /// haptic that becomes Heavy from tier 7.
        /// </summary>
        /// <param name="createdTier">Tier the merge creates.</param>
        [TestCase(1)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(10)]
        public void Merge_OfATier_PlaysThePitchedPopLayerAndHaptic(int createdTier)
        {
            MergeTwo(createdTier - 1);

            var calls = _world.Audio.SfxCalls;
            var big = createdTier >= _feedback.ScreenFx.HeavyMergeMinTier;
            Assert.AreEqual(big ? 2 : 1, calls.Count);
            AssertSfx(calls[0], SfxId.Merge, AudioMath.MergePitch(createdTier, 0), 1f);
            if (big)
            {
                AssertSfx(calls[1], SfxId.MergeBig, 1f, 1f);
            }

            var expected = createdTier >= 7 ? HapticKind.Heavy : HapticKind.Medium;
            CollectionAssert.AreEqual(new[] { expected }, _world.Haptics.Played);
        }

        /// <summary>
        /// A second merge inside the combo window raises the pitch by one semitone.
        /// </summary>
        [Test]
        public void Merge_InACombo_RaisesThePitchOneSemitonePerStep()
        {
            MergeTwo(0);
            _world.Factory.ReleaseAll();
            MergeTwo(0);

            Assert.AreEqual(2, _world.Score.ComboTracker.Combo, "The second merge is in the combo window.");
            var calls = _world.Audio.SfxCalls;
            Assert.AreEqual(2, calls.Count);
            AssertSfx(calls[0], SfxId.Merge, AudioMath.MergePitch(1, 0), 1f);
            AssertSfx(calls[1], SfxId.Merge, AudioMath.MergePitch(1, 1), 1f);
        }

        /// <summary>
        /// Several merges of one step play one sound and one haptic, for the highest tier, and nothing is played
        /// before the director ticks.
        /// </summary>
        [Test]
        public void Merge_ChainInOneStep_PlaysOneBundleWithTheHighestTier()
        {
            Raise(_world.Merge, "Merged", 2, Vector2.zero, Vector2.zero);
            Raise(_world.Merge, "Merged", 5, Vector2.zero, Vector2.zero);
            Raise(_world.Merge, "Merged", 3, Vector2.zero, Vector2.zero);
            Assert.IsEmpty(_world.Audio.SfxCalls, "The sound waits for the end of the step.");

            _world.Feedback.Tick();
            _world.Feedback.Tick();

            Assert.AreEqual(1, _world.Audio.SfxCalls.Count);
            Assert.AreEqual(3, _world.Score.ComboTracker.Combo, "The score system counted the three merges.");
            AssertSfx(_world.Audio.SfxCalls[0], SfxId.Merge, AudioMath.MergePitch(5, 1), 1f);   // The combo step of the merge of tier 5, the second one.
            CollectionAssert.AreEqual(new[] { HapticKind.Medium }, _world.Haptics.Played);
        }

        /// <summary>
        /// Two Black Holes play the big boom and a Heavy haptic, and no merge pop.
        /// </summary>
        [Test]
        public void Supernova_Always_PlaysTheBoomAndAHeavyHaptic()
        {
            MergeTwo(_world.Tiers.Count - 1, () => _world.Audio.SfxCalls.Exists(call => call.Id == SfxId.Supernova));

            Assert.AreEqual(1, _world.Audio.SfxCalls.Count);
            AssertSfx(_world.Audio.SfxCalls[0], SfxId.Supernova, 1f, 1f);
            CollectionAssert.AreEqual(new[] { HapticKind.Heavy }, _world.Haptics.Played);
        }

        /// <summary>
        /// While a piece overflows the danger tick plays at 4 Hz, at 2 Hz with Reduce Shake, and it stops with the
        /// danger.
        /// </summary>
        /// <param name="reduceShake">Whether Reduce Shake is on.</param>
        /// <param name="expectedTicks">Ticks in one second.</param>
        [TestCase(false, 4)]
        [TestCase(true, 2)]
        public void Danger_WhileOverflowing_TicksAtTheRateOfTheSetting(bool reduceShake, int expectedTicks)
        {
            var now = 0.0;
            var director = BindOwnDirector(() => now);
            director.ReduceShake = reduceShake;
            Raise(_world.Overflow, "DangerChanged", true);

            TickFor(director, ref now, DANGER_SECONDS);
            Assert.AreEqual(expectedTicks, CountSfx(SfxId.DangerTick));

            Raise(_world.Overflow, "DangerChanged", false);
            var before = CountSfx(SfxId.DangerTick);
            TickFor(director, ref now, DANGER_SECONDS);
            Assert.AreEqual(before, CountSfx(SfxId.DangerTick), "The tick stops with the danger.");
            director.Unbind();
        }

        /// <summary>
        /// The game-over sound and the long haptic play once, and the danger tick stops.
        /// </summary>
        [Test]
        public void GameOver_Always_PlaysSoundAndLongHapticAndStopsTheDangerTick()
        {
            var now = 0.0;
            var director = BindOwnDirector(() => now);
            Raise(_world.Overflow, "DangerChanged", true);

            _world.Manager.EndRun();

            Assert.AreEqual(1, _world.Audio.SfxCalls.FindAll(call => call.Id == SfxId.GameOver).Count);
            CollectionAssert.AreEqual(new[] { HapticKind.Long }, _world.Haptics.Played);

            _world.Audio.SfxCalls.Clear();
            TickFor(director, ref now, DANGER_SECONDS);
            Assert.AreEqual(0, CountSfx(SfxId.DangerTick), "No tick after the game is over.");
            director.Unbind();
        }

        /// <summary>
        /// At game over the pieces flash from the top down: the higher piece is tinted while the lower one still waits,
        /// and once the flash ends both have their colour back.
        /// </summary>
        [Test]
        public void GameOver_Flash_TintsThePiecesFromTheTopDownAndEnds()
        {
            var low = _world.Factory.Create(_world.Tiers[1], new Vector2(0f, _world.Jar.FloorY + 1f), Vector2.zero);
            var high = _world.Factory.Create(_world.Tiers[1], new Vector2(0f, _world.Jar.DangerLineY - 1f), Vector2.zero);
            var settings = _feedback.Animations;
            var highDelay = settings.GameOverDelay(high.transform.position.y, _world.Jar.DangerLineY, _world.Jar.FloorY);
            var lowDelay = settings.GameOverDelay(low.transform.position.y, _world.Jar.DangerLineY, _world.Jar.FloorY);
            Assert.Less(highDelay + settings.GameOverFlashDuration, lowDelay, "The test needs the flashes not to overlap.");

            _world.Manager.EndRun();
            var time = highDelay + settings.GameOverFlashDuration * 0.5f;
            high.Animator.Tick(time);
            low.Animator.Tick(time);

            Assert.AreNotEqual(Color.white, high.SpriteRenderer.color, "The higher piece flashes first.");
            Assert.AreEqual(Color.white, low.SpriteRenderer.color, "The lower piece still waits.");

            high.Animator.Tick(lowDelay + settings.GameOverFlashDuration);
            low.Animator.Tick(lowDelay + settings.GameOverFlashDuration);

            Assert.AreEqual(Color.white, high.SpriteRenderer.color);
            Assert.AreEqual(Color.white, low.SpriteRenderer.color);
        }

        /// <summary>
        /// A piece that goes back to the pool in the middle of its flash comes out untinted when it is used again.
        /// </summary>
        [Test]
        public void Piece_ReusedAfterAFlash_HasItsColourBack()
        {
            var piece = _world.Factory.Create(_world.Tiers[1], new Vector2(0f, _world.Jar.DangerLineY - 1f), Vector2.zero);
            _world.Manager.EndRun();
            piece.Animator.Tick(_feedback.Animations.GameOverFlashDuration * 0.5f);
            Assert.AreNotEqual(Color.white, piece.SpriteRenderer.color, "The piece is mid-flash.");

            piece.Animator.Begin(piece, _feedback);

            Assert.AreEqual(Color.white, piece.SpriteRenderer.color);
        }
        /// <summary>
        /// The first score above the best plays the fanfare once.
        /// </summary>
        [Test]
        public void NewBest_FirstScoreAboveBest_PlaysTheFanfareOnce()
        {
            _world.Score.BestScore = 0;
            MergeTwo(0);
            _world.Factory.ReleaseAll();
            MergeTwo(1);

            Assert.AreEqual(1, CountSfx(SfxId.NewBest));
        }

        /// <summary>
        /// Nothing plays while the game is paused or over, and a new run goes back to normal.
        /// </summary>
        [Test]
        public void NotPlaying_Events_PlayNothingUntilTheNextRun()
        {
            _world.ScreenFx.ResetCounts();

            Raise(_world.Manager, "StateChanged", GameState.Playing, GameState.Paused);
            Assert.AreEqual(2, _world.ScreenFx.ClearCalls, "Pausing clears the shake and the flash.");
            Assert.AreEqual(1, _world.ScreenFx.CancelCalls, "Pausing cancels the slow-mo.");

            Raise(_world.Controller, "PieceDropped", 1);
            Raise(_world.Merge, "Merged", 9, Vector2.zero, Vector2.zero);
            Raise(_world.Merge, "SupernovaTriggered", Vector2.zero);
            _world.Feedback.Tick();
            Assert.IsEmpty(_world.Audio.SfxCalls);
            Assert.IsEmpty(_world.Haptics.Played);
            Assert.IsEmpty(_world.ScreenFx.Shakes);

            Raise(_world.Manager, "StateChanged", GameState.Paused, GameState.Playing);
            Raise(_world.Controller, "PieceDropped", 1);
            Assert.AreEqual(1, _world.Audio.SfxCalls.Count);
        }

        /// <summary>
        /// Starting a new run drops the merges that were held and cancels the effects in flight.
        /// </summary>
        [Test]
        public void RunStarted_AfterHeldMerges_ForgetsThemAndStopsTheEffects()
        {
            Raise(_world.Merge, "Merged", 9, Vector2.zero, Vector2.zero);
            _world.ScreenFx.ResetCounts();

            _world.Manager.EndRun();
            _world.Audio.SfxCalls.Clear();
            _world.Restart();
            _world.Feedback.Tick();

            Assert.IsEmpty(_world.Audio.SfxCalls.FindAll(call => call.Id == SfxId.Merge), "The held merge is forgotten.");
            Assert.AreEqual(1, _world.ScreenFx.CancelCalls, "The slow-mo of the old run is cancelled.");
        }

        /// <summary>
        /// A director without audio and haptics still runs: it only skips the sounds and the vibrations.
        /// </summary>
        [Test]
        public void Events_WithoutAudioAndHaptics_StillRun()
        {
            _world.Feedback.Unbind();
            var quiet = new FeedbackDirector(
                null, null, _world.FakeParticles, _world.ScreenFx, _world.ScreenFx, _world.ScreenFx,
                _feedback, _world.Tiers, () => 0.0);
            quiet.Bind(_world.Merge, _world.Score, _world.Controller, _world.Overflow, _world.Factory, _world.Manager, _world.Jar);

            Assert.DoesNotThrow(() =>
            {
                Raise(_world.Controller, "PieceDropped", 1);
                Raise(_world.Merge, "Merged", 9, Vector2.zero, Vector2.zero);
                Raise(_world.Overflow, "DangerChanged", true);
                quiet.Tick();
                _world.Manager.EndRun();
            });
            quiet.Unbind();
        }

        /// <summary>
        /// Builds a director on its own clock, bound to the systems of the world after the one of the world is
        /// unbound, so a test can move time without stepping the physics.
        /// </summary>
        /// <param name="clock">The clock of the director.</param>
        /// <returns>The bound director.</returns>
        private FeedbackDirector BindOwnDirector(Func<double> clock)
        {
            _world.Feedback.Unbind();
            var director = new FeedbackDirector(
                _world.Audio, _world.Haptics, _world.FakeParticles, _world.ScreenFx, _world.ScreenFx, _world.ScreenFx,
                _feedback, _world.Tiers, clock);
            director.Bind(_world.Merge, _world.Score, _world.Controller, _world.Overflow, _world.Factory, _world.Manager, _world.Jar);
            return director;
        }

        /// <summary>
        /// Ticks a director every 20 ms of its clock for some seconds.
        /// </summary>
        /// <param name="director">The director.</param>
        /// <param name="now">The clock of the director.</param>
        /// <param name="seconds">Seconds to run.</param>
        private static void TickFor(FeedbackDirector director, ref double now, double seconds)
        {
            var end = now + seconds;
            while (now < end - 1e-9)
            {
                now += 0.02;
                director.Tick();
            }
        }

        /// <summary>
        /// Merges two pieces of a tier and gives the director its end-of-step tick.
        /// </summary>
        /// <param name="tier">Tier of the two pieces.</param>
        /// <param name="alsoDone">An extra stop condition, such as a supernova.</param>
        private void MergeTwo(int tier, Func<bool> alsoDone = null)
        {
            // The overlapping pair would land on each other and add thuds, which these tests do not look at.
            TestReflection.SetField(_feedback.Animations, "_landImpulseThreshold", float.MaxValue);
            MergeScenario.MergeTwo(_world, tier, alsoDone);
        }

        /// <summary>
        /// Counts the sounds of an id that were played.
        /// </summary>
        /// <param name="id">The sound.</param>
        /// <returns>How many times it was played.</returns>
        private int CountSfx(SfxId id)
        {
            return _world.Audio.SfxCalls.FindAll(call => call.Id == id).Count;
        }

        /// <summary>
        /// Asserts the id, pitch and volume of a recorded sound.
        /// </summary>
        /// <param name="call">The recorded call.</param>
        /// <param name="id">The expected sound.</param>
        /// <param name="pitch">The expected pitch.</param>
        /// <param name="volume">The expected volume.</param>
        private static void AssertSfx(FakeAudioService.SfxCall call, SfxId id, float pitch, float volume)
        {
            Assert.AreEqual(id, call.Id);
            Assert.AreEqual(pitch, call.Pitch, TOLERANCE, $"Pitch of {id}.");
            Assert.AreEqual(volume, call.Volume, TOLERANCE, $"Volume of {id}.");
        }

        /// <summary>
        /// Raises an event of an owner by calling the delegate behind it, as the owner would.
        /// </summary>
        /// <param name="owner">The object that owns the event.</param>
        /// <param name="eventName">Name of the event.</param>
        /// <param name="args">Arguments of the event.</param>
        private static void Raise(object owner, string eventName, params object[] args)
        {
            var handler = (Delegate)TestReflection.GetField(owner, eventName);
            handler.DynamicInvoke(args);
        }
    }
}
