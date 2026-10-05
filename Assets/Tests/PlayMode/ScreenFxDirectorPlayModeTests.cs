using System;
using System.Collections.Generic;
using Coika.Data;
using Coika.Fx;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks that <see cref="ScreenFxDirector"/> asks for the shake, the slow-mo and the flash of issue #33 on the
    /// right merges, with the right amplitudes, and that Reduce Shake turns the shake and the slow-mo off, using
    /// recording fakes and real merges of the simulation harness. The fakes never touch the camera or the time scale.
    /// </summary>
    public class ScreenFxDirectorPlayModeTests : HarnessTestBase
    {
        private const float TOLERANCE = 0.0001f;
        private const int HANDLER_CALLS = 1000;

        private SimulationWorld _world;
        private FeedbackConfig _feedback;
        private Recorder _recorder;
        private ScreenFxDirector _director;

        /// <summary>
        /// Builds a world without real particles and a director that records its requests.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _world = new SimulationWorld(new SimulationOptions { Seed = 1, Particles = false });
            _feedback = _world.Config.Feedback;
            _recorder = new Recorder();
            _director = new ScreenFxDirector(_recorder, _recorder, _recorder, _feedback);
            _world.StartRun();
            _director.Bind(_world.Merge);
        }

        /// <summary>
        /// Gives the world back.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _director.Unbind();
            _world.Dispose();
        }

        /// <summary>
        /// A merge that creates tier 8, 9 or 10 shakes with 0.05 x (tier - 7) for 0.2 s and starts one slow-mo of 0.7 for 0.1 s.
        /// </summary>
        /// <param name="createdTier">Tier the merge creates.</param>
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void Merge_OfHeavyTier_ShakesAndSlowsTime(int createdTier)
        {
            MergeFresh(createdTier - 1);

            Assert.AreEqual(1, _recorder.Shakes.Count);
            Assert.AreEqual(0.05f * (createdTier - 7), _recorder.Shakes[0].Amplitude, TOLERANCE);
            Assert.AreEqual(0.2f, _recorder.Shakes[0].Duration, TOLERANCE);
            Assert.AreEqual(1, _recorder.SlowMos.Count);
            Assert.AreEqual(0.7f, _recorder.SlowMos[0].Amplitude, TOLERANCE);
            Assert.AreEqual(0.1f, _recorder.SlowMos[0].Duration, TOLERANCE);
            Assert.AreEqual(0, _recorder.Flashes.Count, "A merge does not flash the screen.");
        }

        /// <summary>
        /// A merge that creates tier 7 or lower asks for nothing.
        /// </summary>
        /// <param name="createdTier">Tier the merge creates.</param>
        [TestCase(1)]
        [TestCase(7)]
        public void Merge_OfLightTier_AsksForNothing(int createdTier)
        {
            MergeFresh(createdTier - 1);

            Assert.AreEqual(0, _recorder.Shakes.Count);
            Assert.AreEqual(0, _recorder.SlowMos.Count);
        }

        /// <summary>
        /// Two Black Holes shake harder than any merge, flash the screen for 0.15 s and do not slow time.
        /// </summary>
        [Test]
        public void Supernova_Always_ShakesAndFlashes()
        {
            MergeFresh(_world.Tiers.Count - 1);

            Assert.AreEqual(1, _recorder.Shakes.Count);
            Assert.AreEqual(_feedback.SupernovaShakeAmplitude, _recorder.Shakes[0].Amplitude, TOLERANCE);
            Assert.Greater(_recorder.Shakes[0].Amplitude, _feedback.MergeShakeAmplitude(_world.Tiers.Count - 1));
            Assert.AreEqual(1, _recorder.Flashes.Count);
            Assert.AreEqual(0.15f, _recorder.Flashes[0], TOLERANCE);
            Assert.AreEqual(0, _recorder.SlowMos.Count);
        }

        /// <summary>
        /// With Reduce Shake on there is no shake and no slow-mo, but the supernova still flashes, and turning it on
        /// stops what is running.
        /// </summary>
        [Test]
        public void ReduceShake_WhenOn_SuppressesShakeAndSlowMoButNotTheFlash()
        {
            _director.ReduceShake = true;
            Assert.AreEqual(1, _recorder.ClearCalls, "The shake is cleared.");
            Assert.AreEqual(1, _recorder.CancelCalls, "The slow-mo is cancelled.");

            MergeFresh(8);
            Assert.AreEqual(0, _recorder.Shakes.Count);
            Assert.AreEqual(0, _recorder.SlowMos.Count);

            MergeFresh(_world.Tiers.Count - 1);
            Assert.AreEqual(0, _recorder.Shakes.Count);
            Assert.AreEqual(1, _recorder.Flashes.Count);
        }

        /// <summary>
        /// StopAll clears the shake and the flash and cancels the slow-mo, as pausing does.
        /// </summary>
        [Test]
        public void StopAll_Always_ClearsShakeAndFlashAndCancelsSlowMo()
        {
            _recorder.Reset();

            _director.StopAll();

            Assert.AreEqual(2, _recorder.ClearCalls, "The shake and the flash are cleared.");
            Assert.AreEqual(1, _recorder.CancelCalls);
        }

        /// <summary>
        /// Binding twice asks for each effect once, and after Unbind nothing is asked for.
        /// </summary>
        [Test]
        public void BindTwice_ThenUnbind_AsksOnceThenNever()
        {
            _director.Bind(_world.Merge);
            MergeFresh(8);
            Assert.AreEqual(1, _recorder.Shakes.Count);

            _director.Unbind();
            MergeFresh(8);
            Assert.AreEqual(0, _recorder.Shakes.Count);
        }

        /// <summary>
        /// The constructor and Bind reject null arguments.
        /// </summary>
        [Test]
        public void ConstructorAndBind_WithNulls_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new ScreenFxDirector(null, _recorder, _recorder, _feedback));
            Assert.Throws<ArgumentNullException>(() => new ScreenFxDirector(_recorder, null, _recorder, _feedback));
            Assert.Throws<ArgumentNullException>(() => new ScreenFxDirector(_recorder, _recorder, null, _feedback));
            Assert.Throws<ArgumentNullException>(() => new ScreenFxDirector(_recorder, _recorder, _recorder, null));
            Assert.Throws<ArgumentNullException>(() => _director.Bind(null));
        }

        /// <summary>
        /// The handlers of both events allocate nothing, called a thousand times with fakes that do nothing.
        /// </summary>
        [Test]
        public void Handlers_CalledManyTimes_AllocateNothing()
        {
            _director.Unbind();
            var quiet = new ScreenFxDirector(NullScreenEffects.Instance, new NullSlowMo(), NullScreenEffects.Instance, _feedback);
            quiet.Bind(_world.Merge);
            var merged = (Action<int, Vector2, Vector2>)TestReflection.GetField(_world.Merge, "Merged");
            var supernova = (Action<Vector2>)TestReflection.GetField(_world.Merge, "SupernovaTriggered");

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < HANDLER_CALLS; i++)
                {
                    merged(9, Vector2.zero, Vector2.zero);
                    supernova(Vector2.zero);
                }
            });

            quiet.Unbind();
            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT);
        }

        /// <summary>
        /// Ten heavy merges in a row never push the real shake and flash cores past the cap or the 3 Hz limit.
        /// </summary>
        [Test]
        public void BurstOfTenHeavyMerges_NeverExceedsTheShakeCapOrTheFlashRate()
        {
            var shake = new ShakeCore(FeedbackConfig.SHAKE_SLOTS, _feedback.ShakeMaxAmplitude, 1);
            var flash = new FlashCore(_feedback.MaxFlashesPerSecond, _feedback.ScreenFlashPeakAlpha);
            var accepted = 0;

            for (var i = 0; i < 10; i++)
            {
                var now = i * 0.05;
                shake.Add(_feedback.MergeShakeAmplitude(10), _feedback.ShakeDuration, now);
                shake.Add(_feedback.SupernovaShakeAmplitude, _feedback.SupernovaShakeDuration, now);
                accepted += flash.TryFlash(_feedback.ScreenFlashDuration, now) ? 1 : 0;
                var offset = shake.Evaluate(now);
                Assert.LessOrEqual(Mathf.Abs(offset.x), _feedback.ShakeMaxAmplitude + TOLERANCE);
                Assert.LessOrEqual(Mathf.Abs(offset.y), _feedback.ShakeMaxAmplitude + TOLERANCE);
            }

            Assert.LessOrEqual(accepted, Mathf.CeilToInt(_feedback.MaxFlashesPerSecond * 0.5f), "At most 3 flashes per second over half a second of requests.");
        }

        /// <summary>
        /// Forgets the earlier pieces and requests, then merges two pieces of the tier.
        /// </summary>
        /// <param name="tier">Tier of the two pieces.</param>
        private void MergeFresh(int tier)
        {
            _world.Factory.ReleaseAll();
            _recorder.Reset();
            MergeScenario.MergeTwo(_world, tier, () => _recorder.Flashes.Count > 0);
        }

        /// <summary>
        /// A slow-mo that ignores every request.
        /// </summary>
        private sealed class NullSlowMo : ISlowMo
        {
            /// <inheritdoc />
            public void Begin(float scale, float duration)
            {
            }

            /// <inheritdoc />
            public void Cancel()
            {
            }
        }

        /// <summary>
        /// Records every request to the shake, the slow-mo and the flash.
        /// </summary>
        private sealed class Recorder : IScreenShake, ISlowMo, IScreenFlash
        {
            /// <summary>The shakes asked for.</summary>
            public List<Request> Shakes { get; } = new();

            /// <summary>The slow-mos asked for; the amplitude is the scale.</summary>
            public List<Request> SlowMos { get; } = new();

            /// <summary>The durations of the flashes asked for.</summary>
            public List<float> Flashes { get; } = new();

            /// <summary>How many times the shake was cleared.</summary>
            public int ClearCalls { get; private set; }

            /// <summary>How many times the slow-mo was cancelled.</summary>
            public int CancelCalls { get; private set; }

            /// <inheritdoc />
            public void Shake(float amplitude, float duration)
            {
                Shakes.Add(new Request(amplitude, duration));
            }

            /// <inheritdoc />
            public void Clear()
            {
                ClearCalls++;
            }

            /// <inheritdoc />
            public void Begin(float scale, float duration)
            {
                SlowMos.Add(new Request(scale, duration));
            }

            /// <inheritdoc />
            public void Cancel()
            {
                CancelCalls++;
            }

            /// <inheritdoc />
            public void Flash(float duration)
            {
                Flashes.Add(duration);
            }

            /// <summary>
            /// Forgets the requests, keeping the counts of clears and cancels.
            /// </summary>
            public void Reset()
            {
                Shakes.Clear();
                SlowMos.Clear();
                Flashes.Clear();
            }
        }

        /// <summary>
        /// One recorded request.
        /// </summary>
        private readonly struct Request
        {
            /// <summary>Creates the record.</summary>
            /// <param name="amplitude">Amplitude of a shake, or scale of a slow-mo.</param>
            /// <param name="duration">Seconds it lasts.</param>
            public Request(float amplitude, float duration)
            {
                Amplitude = amplitude;
                Duration = duration;
            }

            /// <summary>Amplitude of a shake, or scale of a slow-mo.</summary>
            public float Amplitude { get; }

            /// <summary>Seconds it lasts.</summary>
            public float Duration { get; }
        }
    }
}
