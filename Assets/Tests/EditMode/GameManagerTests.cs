using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the <see cref="GameManager"/> (issue #11): the states, the events, the rejected calls and the wait
    /// before the Game Over view, all against a fake of the gameplay systems so no scene is needed.
    /// </summary>
    public class GameManagerTests
    {
        private const float TOLERANCE = 0.0001f;

        private readonly List<UnityEngine.Object> _created = new();

        private FakeRunSystems _systems;
        private GameManager _manager;
        private int _nextSeed;

        /// <summary>
        /// Builds a manager in the boot state with a fake that hands out a new seed each time.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            var config = TestGameConfig.CreateWithSpawn(new[] { 1f, 1f, 1f }, 3, 2, new[] { 0, 1, 0 });
            _created.Add(config);
            _systems = new FakeRunSystems(config);
            _nextSeed = 100;
            _manager = new GameManager(_systems, () => _nextSeed++);
        }

        /// <summary>
        /// Destroys the assets the test created.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        /// <summary>
        /// A new manager waits in the boot state and has touched nothing.
        /// </summary>
        [Test]
        public void New_Always_StartsInBootWithoutTouchingTheSystems()
        {
            Assert.AreEqual(GameState.Boot, _manager.State);
            Assert.AreEqual(0, _systems.Prepared);
        }

        /// <summary>
        /// Starting a run prepares the systems with the seed, lets them play, enters Playing and announces it, in
        /// that order.
        /// </summary>
        [Test]
        public void StartRun_FromBoot_PreparesPlaysAndEntersPlaying()
        {
            GameState? seenState = null;
            RunContext seenRun = null;
            _manager.StateChanged += (from, to) => seenState = to;
            _manager.RunStarted += run => seenRun = run;

            _manager.StartRun();

            Assert.AreEqual(GameState.Playing, _manager.State);
            Assert.AreEqual(GameState.Playing, seenState);
            Assert.AreEqual(new[] { "Prepare:100", "Begin" }, _systems.Calls.ToArray());
            Assert.IsNotNull(seenRun);
            Assert.AreEqual(_systems.LastContext, seenRun);
        }

        /// <summary>
        /// The state change reports the old state as well as the new one.
        /// </summary>
        [Test]
        public void StateChanged_OnStartRun_ReportsFromAndTo()
        {
            GameState from = GameState.Menu;
            GameState to = GameState.Menu;
            _manager.StateChanged += (a, b) =>
            {
                from = a;
                to = b;
            };

            _manager.StartRun();

            Assert.AreEqual(GameState.Boot, from);
            Assert.AreEqual(GameState.Playing, to);
        }

        /// <summary>
        /// Starting while a run is in progress restarts it with a new seed instead of failing.
        /// </summary>
        [Test]
        public void StartRun_WhilePlaying_RestartsCleanlyWithANewSeed()
        {
            _manager.StartRun();

            _manager.StartRun();

            Assert.AreEqual(GameState.Playing, _manager.State);
            Assert.AreEqual(new[] { "Prepare:100", "Begin", "Prepare:101", "Begin" }, _systems.Calls.ToArray());
        }

        /// <summary>
        /// Ending a run stops the systems, enters GameOver and announces the summary.
        /// </summary>
        [Test]
        public void EndRun_WhilePlaying_StopsTheSystemsAndEntersGameOver()
        {
            _manager.StartRun();
            RunSummary? ended = null;
            _manager.RunEnded += summary => ended = summary;

            _manager.EndRun();

            Assert.AreEqual(GameState.GameOver, _manager.State);
            Assert.AreEqual("Stop", _systems.Calls[_systems.Calls.Count - 1]);
            Assert.IsTrue(ended.HasValue);
            Assert.AreEqual(_systems.Summary.Score, ended.Value.Score);
        }

        /// <summary>
        /// Two pieces overflowing in the same step end the run once: the second call changes nothing and
        /// announces nothing.
        /// </summary>
        [Test]
        public void EndRun_CalledTwice_StopsAndAnnouncesOnce()
        {
            _manager.StartRun();
            var ended = 0;
            var changes = 0;
            _manager.RunEnded += summary => ended++;
            _manager.StateChanged += (from, to) => changes++;

            _manager.EndRun();
            _manager.EndRun();

            Assert.AreEqual(1, _systems.Stopped);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(1, changes);
        }

        /// <summary>
        /// Ending a run that never started is rejected with a warning and changes nothing.
        /// </summary>
        [Test]
        public void EndRun_BeforeAnyRun_IsRejectedWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("EndRun ignored"));

            _manager.EndRun();

            Assert.AreEqual(GameState.Boot, _manager.State);
            Assert.AreEqual(0, _systems.Stopped);
        }

        /// <summary>
        /// Retry from the Game Over state starts a new run with a new seed.
        /// </summary>
        [Test]
        public void Retry_FromGameOver_StartsANewRun()
        {
            _manager.StartRun();
            _manager.EndRun();

            _manager.Retry();

            Assert.AreEqual(GameState.Playing, _manager.State);
            Assert.AreEqual(2, _systems.Prepared);
            Assert.AreEqual(101, _systems.LastSeed);
        }

        /// <summary>
        /// Retry while playing is rejected with a warning and does not restart the run.
        /// </summary>
        [Test]
        public void Retry_WhilePlaying_IsRejectedWithAWarning()
        {
            _manager.StartRun();
            LogAssert.Expect(LogType.Warning, new Regex("Retry ignored"));

            _manager.Retry();

            Assert.AreEqual(1, _systems.Prepared);
            Assert.AreEqual(GameState.Playing, _manager.State);
        }

        /// <summary>
        /// Pausing a run and resuming it goes Playing, Paused, Playing and touches the systems not at all.
        /// </summary>
        [Test]
        public void PauseResume_WhilePlaying_ChangesOnlyTheStateAndLeavesTheSystemsAlone()
        {
            _manager.StartRun();
            var calls = _systems.Calls.Count;
            var states = new List<GameState>();
            _manager.StateChanged += (from, to) => states.Add(to);

            _manager.Pause();
            _manager.Resume();

            Assert.AreEqual(new[] { GameState.Paused, GameState.Playing }, states.ToArray());
            Assert.AreEqual(calls, _systems.Calls.Count);
            Assert.AreEqual(1, _systems.Prepared);
        }

        /// <summary>
        /// Pause outside a run is rejected with a warning.
        /// </summary>
        [Test]
        public void Pause_BeforeAnyRun_IsRejectedWithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Pause ignored"));

            _manager.Pause();

            Assert.AreEqual(GameState.Boot, _manager.State);
        }

        /// <summary>
        /// Resume while playing is rejected with a warning.
        /// </summary>
        [Test]
        public void Resume_WhilePlaying_IsRejectedWithAWarning()
        {
            _manager.StartRun();
            LogAssert.Expect(LogType.Warning, new Regex("Resume ignored"));

            _manager.Resume();

            Assert.AreEqual(GameState.Playing, _manager.State);
        }

        /// <summary>
        /// Restarting from the pause menu is a StartRun: it prepares a new run and leaves Paused for Playing.
        /// </summary>
        [Test]
        public void StartRun_WhilePaused_PreparesANewRunAndPlays()
        {
            _manager.StartRun();
            _manager.Pause();

            _manager.StartRun();

            Assert.AreEqual(GameState.Playing, _manager.State);
            Assert.AreEqual(2, _systems.Prepared);
        }

        /// <summary>
        /// Retry can be repeated: every game over allows one more run.
        /// </summary>
        [Test]
        public void Retry_ThreeRuns_EachPreparesFromScratch()
        {
            _manager.StartRun();
            for (var i = 0; i < 2; i++)
            {
                _manager.EndRun();
                _manager.Retry();
            }

            Assert.AreEqual(3, _systems.Prepared);
            Assert.AreEqual(2, _systems.Stopped);
            Assert.AreEqual(GameState.Playing, _manager.State);
        }

        /// <summary>
        /// The Game Over view is announced only after 1.2 seconds of unscaled time, and once.
        /// </summary>
        [Test]
        public void Tick_AfterTheDelay_RaisesGameOverReadyOnce()
        {
            _manager.StartRun();
            var ready = 0;
            _manager.GameOverReady += summary => ready++;
            _manager.EndRun();

            _manager.Tick(GameManager.GAME_OVER_DELAY - 0.1f);
            Assert.AreEqual(0, ready);

            _manager.Tick(0.1f + TOLERANCE);
            Assert.AreEqual(1, ready);

            _manager.Tick(5f);
            Assert.AreEqual(1, ready);
        }

        /// <summary>
        /// Ticking while a run is in progress announces nothing.
        /// </summary>
        [Test]
        public void Tick_WhilePlaying_RaisesNothing()
        {
            _manager.StartRun();
            var ready = 0;
            _manager.GameOverReady += summary => ready++;

            _manager.Tick(10f);

            Assert.AreEqual(0, ready);
        }

        /// <summary>
        /// Retrying before the delay is over cancels the pending announcement, so the view of the dead run never
        /// appears over the new one.
        /// </summary>
        [Test]
        public void Retry_BeforeTheDelayEnds_CancelsTheGameOverView()
        {
            _manager.StartRun();
            var ready = 0;
            _manager.GameOverReady += summary => ready++;
            _manager.EndRun();
            _manager.Tick(0.5f);

            _manager.Retry();
            _manager.Tick(5f);

            Assert.AreEqual(0, ready);
        }

        /// <summary>
        /// The manager needs its dependencies.
        /// </summary>
        [Test]
        public void New_WithANullDependency_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GameManager(null, () => 0));
            Assert.Throws<ArgumentNullException>(() => new GameManager(_systems, null));
        }

        /// <summary>
        /// A fake of the gameplay systems that records the calls.
        /// </summary>
        private sealed class FakeRunSystems : IRunSystems
        {
            private readonly GameConfig _config;

            /// <summary>
            /// Creates the fake.
            /// </summary>
            /// <param name="config">Config of the queue and the score the fake builds.</param>
            public FakeRunSystems(GameConfig config)
            {
                _config = config;
            }

            /// <summary>The calls in order, like <c>Prepare:100</c>, <c>Begin</c> and <c>Stop</c>.</summary>
            public List<string> Calls { get; } = new();

            /// <summary>Times a run was prepared.</summary>
            public int Prepared { get; private set; }

            /// <summary>Times the run was stopped.</summary>
            public int Stopped { get; private set; }

            /// <summary>Seed of the last prepared run.</summary>
            public int LastSeed { get; private set; }

            /// <summary>The context handed out by the last <see cref="PrepareRun"/>.</summary>
            public RunContext LastContext { get; private set; }

            /// <summary>The summary every stop returns.</summary>
            public RunSummary Summary { get; } = new RunSummary(42, 42, true, 3, 7, 12.5f);

            /// <inheritdoc />
            public RunContext PrepareRun(int seed)
            {
                Prepared++;
                LastSeed = seed;
                Calls.Add($"Prepare:{seed}");
                var tiers = new List<TierDefinition>();
                LastContext = new RunContext(new ScoreSystem(_config, tiers, () => 0d), new SpawnQueue(_config, seed), tiers, new FakeAssetService());
                return LastContext;
            }

            /// <inheritdoc />
            public void BeginPlaying()
            {
                Calls.Add("Begin");
            }

            /// <inheritdoc />
            public RunSummary StopPlaying()
            {
                Stopped++;
                Calls.Add("Stop");
                return Summary;
            }
        }
    }
}
