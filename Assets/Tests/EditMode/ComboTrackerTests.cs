using System;
using System.Collections.Generic;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the <see cref="ComboTracker"/> (issue #8): when a merge continues or restarts the combo, the
    /// multiplier and its cap, the expiry, the events, and that it allocates nothing. The time is always passed in,
    /// so the tests are exact.
    /// </summary>
    public class ComboTrackerTests
    {
        private const float TOLERANCE = 0.0001f;
        private const int LOOP_COUNT = 1000;

        private readonly List<UnityEngine.Object> _created = new();
        private readonly List<(int Combo, float Multiplier)> _events = new();
        private GameConfig _config;
        private ComboTracker _tracker;

        /// <summary>
        /// Creates a tracker with the default combo values (window 1 s, step 0.25, cap ×3) and records its events.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, null);
            _tracker = new ComboTracker(_config);
            _tracker.ComboChanged += (combo, multiplier) => _events.Add((combo, multiplier));
            _events.Clear();
        }

        /// <summary>
        /// Destroys the configs, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Before any merge the combo is 0 and the multiplier 1.
        /// </summary>
        [Test]
        public void NewTracker_HasNoComboAndMultiplierOne()
        {
            Assert.AreEqual(0, _tracker.Combo);
            Assert.AreEqual(1f, _tracker.Multiplier, TOLERANCE);
        }

        /// <summary>
        /// Merges at 0, 0.5 and 0.9 s give multipliers 1.0, 1.25 and 1.5, and a merge at 2.5 s starts again at 1.0.
        /// </summary>
        [Test]
        public void RegisterMerge_AtTheTimesOfTheIssue_GivesTheExpectedMultipliers()
        {
            _tracker.RegisterMerge(0d);
            Assert.AreEqual(1f, _tracker.Multiplier, TOLERANCE, "t = 0");

            _tracker.RegisterMerge(0.5d);
            Assert.AreEqual(1.25f, _tracker.Multiplier, TOLERANCE, "t = 0.5");

            _tracker.RegisterMerge(0.9d);
            Assert.AreEqual(1.5f, _tracker.Multiplier, TOLERANCE, "t = 0.9");

            _tracker.RegisterMerge(2.5d);
            Assert.AreEqual(1f, _tracker.Multiplier, TOLERANCE, "t = 2.5 starts again");
            Assert.AreEqual(1, _tracker.Combo, "Combo after the restart");
        }

        /// <summary>
        /// A merge exactly one window after the previous one still continues the combo; the window is inclusive.
        /// </summary>
        [Test]
        public void RegisterMerge_ExactlyAtTheWindowEdge_ContinuesTheCombo()
        {
            _tracker.RegisterMerge(3d);
            _tracker.RegisterMerge(4d);

            Assert.AreEqual(2, _tracker.Combo);
        }

        /// <summary>
        /// The window is measured from the previous merge, not from the first one, so a long chain of close merges
        /// goes on.
        /// </summary>
        [Test]
        public void RegisterMerge_ChainOfCloseMerges_KeepsGrowingPastTheFirstWindow()
        {
            for (var i = 0; i < 5; i++)
            {
                _tracker.RegisterMerge(i * 0.8d);
            }

            Assert.AreEqual(5, _tracker.Combo);
            Assert.AreEqual(2f, _tracker.Multiplier, TOLERANCE);
        }

        /// <summary>
        /// After 20 consecutive merges the multiplier is the cap, ×3, and never above it.
        /// </summary>
        [Test]
        public void Multiplier_AfterTwentyConsecutiveMerges_IsCappedAtThree()
        {
            for (var i = 0; i < 20; i++)
            {
                _tracker.RegisterMerge(i * 0.1d);
                Assert.LessOrEqual(_tracker.Multiplier, 3f + TOLERANCE, $"Merge {i + 1}");
            }

            Assert.AreEqual(20, _tracker.Combo);
            Assert.AreEqual(3f, _tracker.Multiplier, TOLERANCE);
        }

        /// <summary>
        /// The window, the step and the cap come from the config, not from constants.
        /// </summary>
        [Test]
        public void Constructor_WithOtherConfigValues_UsesThem()
        {
            var tracker = new ComboTracker(CreateScoringConfig(2f, 0.5f, 2));

            tracker.RegisterMerge(0d);
            tracker.RegisterMerge(1.9d);
            Assert.AreEqual(2, tracker.Combo, "A merge 1.9 s later is inside a 2 s window.");
            Assert.AreEqual(1.5f, tracker.Multiplier, TOLERANCE, "Step 0.5.");

            // Combo 4 would be ×2.5 by the step, so the cap of 2 is what limits it.
            tracker.RegisterMerge(3.8d);
            tracker.RegisterMerge(5.7d);
            Assert.AreEqual(4, tracker.Combo);
            Assert.AreEqual(2f, tracker.Multiplier, TOLERANCE, "Cap 2.");
        }

        /// <summary>
        /// A merge raises the event with the new combo and multiplier.
        /// </summary>
        [Test]
        public void RegisterMerge_Always_RaisesComboChangedWithComboAndMultiplier()
        {
            _tracker.RegisterMerge(0d);
            _tracker.RegisterMerge(0.5d);

            Assert.AreEqual(2, _events.Count);
            Assert.AreEqual(1, _events[0].Combo, "First combo");
            Assert.AreEqual(1f, _events[0].Multiplier, TOLERANCE, "First multiplier");
            Assert.AreEqual(2, _events[1].Combo, "Second combo");
            Assert.AreEqual(1.25f, _events[1].Multiplier, TOLERANCE, "Second multiplier");
        }

        /// <summary>
        /// While the window is open a tick changes nothing.
        /// </summary>
        [Test]
        public void Tick_WithinTheWindow_KeepsTheCombo()
        {
            _tracker.RegisterMerge(0d);
            _events.Clear();

            _tracker.Tick(1d);

            Assert.AreEqual(1, _tracker.Combo);
            Assert.AreEqual(0, _events.Count);
        }

        /// <summary>
        /// When the window passes without a merge the combo ends, once, with combo 0 and ×1.
        /// </summary>
        [Test]
        public void Tick_AfterTheWindow_EndsTheComboAndRaisesTheEventOnce()
        {
            _tracker.RegisterMerge(0d);
            _tracker.RegisterMerge(0.5d);
            _events.Clear();

            _tracker.Tick(1.6d);
            _tracker.Tick(1.7d);

            Assert.AreEqual(0, _tracker.Combo);
            Assert.AreEqual(1f, _tracker.Multiplier, TOLERANCE);
            Assert.AreEqual(1, _events.Count, "Only the first tick ends the combo.");
            Assert.AreEqual(0, _events[0].Combo);
            Assert.AreEqual(1f, _events[0].Multiplier, TOLERANCE);
        }

        /// <summary>
        /// With no combo a tick does nothing, however late it is.
        /// </summary>
        [Test]
        public void Tick_WithoutACombo_RaisesNothing()
        {
            _tracker.Tick(100d);

            Assert.AreEqual(0, _tracker.Combo);
            Assert.AreEqual(0, _events.Count);
        }

        /// <summary>
        /// A merge after the combo expired starts a new one at 1.
        /// </summary>
        [Test]
        public void RegisterMerge_AfterTheComboExpired_StartsAgainAtOne()
        {
            _tracker.RegisterMerge(0d);
            _tracker.RegisterMerge(0.5d);
            _tracker.Tick(2d);

            _tracker.RegisterMerge(2.1d);

            Assert.AreEqual(1, _tracker.Combo);
        }

        /// <summary>
        /// Resetting clears the combo and raises no event.
        /// </summary>
        [Test]
        public void Reset_AfterAComboOfTwo_ClearsItWithoutEvents()
        {
            _tracker.RegisterMerge(0d);
            _tracker.RegisterMerge(0.5d);
            _events.Clear();

            _tracker.Reset();

            Assert.AreEqual(0, _tracker.Combo);
            Assert.AreEqual(1f, _tracker.Multiplier, TOLERANCE);
            Assert.AreEqual(0, _events.Count);
        }

        /// <summary>
        /// A missing config is a programming error and throws.
        /// </summary>
        [Test]
        public void Constructor_WithNullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ComboTracker(null));
        }

        /// <summary>
        /// Registering merges, ticking and reading the multiplier allocate nothing in steady state.
        /// </summary>
        [Test]
        public void RegisterMergeAndTick_InSteadyState_AllocateNothing()
        {
            var tracker = new ComboTracker(_config);
            tracker.ComboChanged += (_, _) => { };
            tracker.RegisterMerge(0d);
            tracker.Tick(5d);
            var sink = 0f;

            var allocated = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < LOOP_COUNT; i++)
                {
                    tracker.RegisterMerge(i * 0.4d);
                    sink += tracker.Multiplier;
                    tracker.Tick(i * 0.4d + 2d);
                }
            });

            Assert.Greater(sink, 0f);
            Assert.LessOrEqual(allocated, AllocationMeter.TOLERANCE_COUNT, "Managed allocations made by the combo tracker.");
        }

        /// <summary>
        /// Creates a config with the given combo values and the default Supernova bonus, and destroys it at the end
        /// of the test.
        /// </summary>
        private GameConfig CreateScoringConfig(float comboWindow, float comboStep, int comboCap)
        {
            var config = TestGameConfig.CreateWithScoring(comboWindow, comboStep, comboCap, 500);
            _created.Add(config);
            return config;
        }
    }
}
