using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using Coika.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Plays the real Game scene (issue #11): loads it through Addressables, drives whole runs through the
    /// <see cref="GameManager"/> and checks that nothing of one run survives into the next. Run with the Addressables
    /// Play Mode Script set to "Use Asset Database".
    /// </summary>
    public class GameLoopPlayModeTests
    {
        private const string SCENE_KEY = "Assets/Scenes/GameScene.unity";
        private const float LOAD_TIMEOUT = 20f;
        private const float DELAY_MARGIN = 0.15f;
        private const float MERGE_OVERLAP = 0.45f;

        private SceneLoaderService _loader;
        private GameSceneInstaller _installer;

        /// <summary>
        /// Loads the Game scene and waits until the first run is playing.
        /// </summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _loader = new SceneLoaderService();
            var load = _loader.LoadScene(SCENE_KEY, LoadSceneMode.Additive);
            yield return new WaitUntil(() => load.IsCompleted);
            Assert.IsFalse(load.IsFaulted, load.Exception?.ToString());

            _installer = UnityEngine.Object.FindAnyObjectByType<GameSceneInstaller>();
            Assert.IsNotNull(_installer, "The Game scene needs a GameSceneInstaller.");
            yield return WaitUntilPlaying();
        }

        /// <summary>
        /// Unloads the scene, which releases everything the installer loaded, and gives the physics back.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_loader.IsSceneLoaded(SCENE_KEY))
            {
                var unload = _loader.UnloadScene(SCENE_KEY);
                yield return new WaitUntil(() => unload.IsCompleted);
            }

            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        /// <summary>
        /// The scene starts a run by itself, with the frame rate of the GDD and the physics running.
        /// </summary>
        [Test]
        public void Scene_AfterLoading_IsPlayingWithTheBootSettings()
        {
            Assert.AreEqual(GameState.Playing, _installer.Manager.State);
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            Assert.AreEqual(SimulationMode2D.FixedUpdate, Physics2D.simulationMode);
            Assert.IsFalse(_installer.Overlay.IsVisible);
        }

        /// <summary>
        /// Three consecutive runs end with the same number of pieces and event subscribers as the first, an empty
        /// board, a zero score and no combo.
        /// </summary>
        [UnityTest]
        public IEnumerator Retry_ThreeConsecutiveRuns_LeaveNoPiecesOrSubscribers()
        {
            var factory = Field<PieceFactory>(_installer, "_factory");
            var score = Field<ScoreSystem>(_installer, "_score");
            var baselinePieces = factory.ActivePieces.Count;
            var baselineSubscribers = CountSubscribers();
            Assert.Greater(baselineSubscribers, 0, "The subscriber count would prove nothing.");

            for (var run = 0; run < 3; run++)
            {
                FillBoard(factory);
                Assert.Greater(factory.ActivePieces.Count, baselinePieces);

                _installer.Manager.EndRun();
                Assert.AreEqual(GameState.GameOver, _installer.Manager.State);

                _installer.Manager.Retry();
                yield return null;

                Assert.AreEqual(GameState.Playing, _installer.Manager.State);
                Assert.AreEqual(baselinePieces, factory.ActivePieces.Count, $"Run {run + 2} started with leftover pieces.");
                Assert.AreEqual(baselineSubscribers, CountSubscribers(), $"Run {run + 2} changed the event subscribers.");
                Assert.AreEqual(0, score.Score);
                Assert.AreEqual(1f, score.ComboTracker.Multiplier);
            }
        }

        /// <summary>
        /// While the game is over the drop, the merges and the detector are off and the physics is frozen, so nothing
        /// can be dropped and the score cannot change.
        /// </summary>
        [UnityTest]
        public IEnumerator GameOver_WhileOver_NothingCanDropOrScore()
        {
            var controller = Field<DropController>(_installer, "_dropController");
            var merge = Field<MergeSystem>(_installer, "_mergeSystem");
            var detector = Field<OverflowDetector>(_installer, "_overflowDetector");
            var score = Field<ScoreSystem>(_installer, "_score");
            Assert.IsTrue(controller.IsEnabled);

            _installer.Manager.EndRun();
            yield return null;

            Assert.IsFalse(controller.IsEnabled);
            Assert.IsFalse(merge.enabled);
            Assert.IsFalse(detector.IsRunning);
            Assert.AreEqual(SimulationMode2D.Script, Physics2D.simulationMode);

            var before = score.Score;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(before, score.Score);
        }

        /// <summary>
        /// The Game Over view appears after the 1.2 second wait, not before, and the Retry button starts a new run
        /// and hides it again.
        /// </summary>
        [UnityTest]
        public IEnumerator GameOver_AfterTheDelay_ShowsTheViewAndRetryHidesIt()
        {
            var view = UnityEngine.Object.FindAnyObjectByType<GameOverView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view);
            Assert.IsFalse(view.gameObject.activeSelf);

            _installer.Manager.EndRun();
            yield return new WaitForSecondsRealtime(GameManager.GAME_OVER_DELAY - DELAY_MARGIN * 2f);
            Assert.IsFalse(view.gameObject.activeSelf, "The view appeared before the delay.");

            yield return new WaitForSecondsRealtime(DELAY_MARGIN * 4f);
            Assert.IsTrue(view.gameObject.activeSelf, "The view did not appear after the delay.");

            var retry = Field<Button>(view, "_retryButton");
            retry.onClick.Invoke();
            Assert.AreEqual(GameState.GameOver, _installer.Manager.State, "Retry was accepted during the lock.");

            yield return new WaitForSecondsRealtime(UiAnimation.RETRY_LOCK_SECONDS + DELAY_MARGIN);
            retry.onClick.Invoke();
            yield return null;

            Assert.AreEqual(GameState.Playing, _installer.Manager.State);
            Assert.IsFalse(view.gameObject.activeSelf);
        }

        /// <summary>
        /// A run that scores and ends in the real scene folds its score into the save as the new best, and a new save
        /// system on the same folder (a restart of the game) reads it back (issue #40). The write itself is the job of
        /// <c>SaveTriggers</c> in the Boot scene, so the test flushes the save as it would.
        /// </summary>
        [UnityTest]
        public IEnumerator GameOver_AfterAScoringRun_PersistsTheBestScoreAcrossARestart()
        {
            var directory = Path.Combine(Path.GetTempPath(), "coika-gameover-" + Guid.NewGuid().ToString("N"));
            try
            {
                var save = new SaveSystem(new FileSaveStorage(directory));
                save.Load();
                _installer.UseSave(save);

                var factory = Field<PieceFactory>(_installer, "_factory");
                var score = Field<ScoreSystem>(_installer, "_score");
                var tier = Field<IReadOnlyList<TierDefinition>>(_installer, "_tiers")[0];
                var offset = tier.Radius * MERGE_OVERLAP;
                factory.Create(tier, new Vector2(-offset, 0f), Vector2.zero);
                factory.Create(tier, new Vector2(offset, 0f), Vector2.zero);

                var until = Time.realtimeSinceStartup + LOAD_TIMEOUT;
                yield return new WaitUntil(() => score.Merges > 0 || Time.realtimeSinceStartup > until);
                Assert.Greater(score.Score, 0, "The pair did not merge, so the run scored nothing.");

                var finalScore = score.Score;
                _installer.Manager.EndRun();
                save.FlushIfDirty();

                var restarted = new SaveSystem(new FileSaveStorage(directory));
                restarted.Load();
                Assert.AreEqual(finalScore, restarted.Data.bestScore.classic);
                Assert.AreEqual(1, restarted.Data.totals.games);
                Assert.AreEqual(score.Merges, restarted.Data.totals.merges);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        /// <summary>
        /// A game over ducks the music by 6 dB and the retry restores it and plays the gameplay loop (issue #29).
        /// </summary>
        [UnityTest]
        public IEnumerator GameOver_WithAudio_DucksTheMusicAndRetryRestoresIt()
        {
            var audio = new FakeAudioService();
            _installer.UseAudio(audio);

            _installer.Manager.EndRun();
            Assert.AreEqual(1, audio.DuckCalls.Count);
            Assert.AreEqual(-6f, audio.DuckCalls[0].Db);

            yield return new WaitForSecondsRealtime(GameManager.GAME_OVER_DELAY + DELAY_MARGIN);
            _installer.Manager.Retry();

            Assert.AreEqual(2, audio.DuckCalls.Count);
            Assert.AreEqual(0f, audio.DuckCalls[1].Db);
            CollectionAssert.AreEqual(new[] { MusicId.Gameplay }, audio.MusicCalls);
        }

        /// <summary>
        /// Unloading the Game scene and loading it again plays a fresh run.
        /// </summary>
        [UnityTest]
        public IEnumerator Scene_UnloadedAndReloaded_PlaysAgain()
        {
            var unload = _loader.UnloadScene(SCENE_KEY);
            yield return new WaitUntil(() => unload.IsCompleted);
            yield return null;
            Assert.AreEqual(SimulationMode2D.FixedUpdate, Physics2D.simulationMode);

            var load = _loader.LoadScene(SCENE_KEY, LoadSceneMode.Additive);
            yield return new WaitUntil(() => load.IsCompleted);
            Assert.IsFalse(load.IsFaulted, load.Exception?.ToString());

            _installer = UnityEngine.Object.FindAnyObjectByType<GameSceneInstaller>();
            yield return WaitUntilPlaying();

            Assert.AreEqual(GameState.Playing, _installer.Manager.State);
        }

        /// <summary>
        /// An installer that cannot load shows the Retry button, logs the error and does not spin forever.
        /// </summary>
        [UnityTest]
        public IEnumerator Installer_WithMissingReferences_ShowsTheRetryUi()
        {
            LogAssert.Expect(LogType.Error, new Regex("could not load the game"));
            var host = new GameObject("BrokenInstaller");
            host.SetActive(false);
            var broken = host.AddComponent<GameSceneInstaller>();
            host.SetActive(true);

            var until = Time.realtimeSinceStartup + LOAD_TIMEOUT;
            yield return new WaitUntil(() => broken.Overlay.IsShowingFailure || Time.realtimeSinceStartup > until);

            Assert.IsTrue(broken.Overlay.IsShowingFailure);
            Assert.IsNull(broken.Manager);

            UnityEngine.Object.Destroy(host);
        }

        /// <summary>
        /// Waits until the installer built the manager and the first run is playing.
        /// </summary>
        private IEnumerator WaitUntilPlaying()
        {
            var until = Time.realtimeSinceStartup + LOAD_TIMEOUT;
            yield return new WaitUntil(() => (_installer.Manager != null && _installer.Manager.State == GameState.Playing) || Time.realtimeSinceStartup > until);
            Assert.IsNotNull(_installer.Manager, "The game did not finish loading.");
            Assert.AreEqual(GameState.Playing, _installer.Manager.State);
        }

        /// <summary>
        /// Puts a few pieces in the jar, as a run in progress would have.
        /// </summary>
        private void FillBoard(PieceFactory factory)
        {
            var tiers = Field<IReadOnlyList<TierDefinition>>(_installer, "_tiers");
            for (var i = 0; i < 3; i++)
            {
                factory.Create(tiers[i], new Vector2(i - 1f, 0f), Vector2.zero);
            }
        }

        /// <summary>
        /// Counts the listeners of the events that a run connects, across the systems of the scene.
        /// </summary>
        private int CountSubscribers()
        {
            var score = Field<ScoreSystem>(_installer, "_score");
            return Count(Field<OverflowDetector>(_installer, "_overflowDetector"), "GameOverTriggered")
                + Count(Field<OverflowDetector>(_installer, "_overflowDetector"), "OverflowProgressChanged")
                + Count(Field<MergeSystem>(_installer, "_mergeSystem"), "Merged")
                + Count(Field<MergeSystem>(_installer, "_mergeSystem"), "SupernovaTriggered")
                + Count(Field<OverflowDetector>(_installer, "_overflowDetector"), "DangerChanged")
                + Count(Field<DropController>(_installer, "_dropController"), "PieceSpawned")
                + Count(Field<DropController>(_installer, "_dropController"), "PieceDropped")
                + Count(Field<DropController>(_installer, "_dropController"), "StateChanged")
                + Count(Field<PieceFactory>(_installer, "_factory"), "PieceCreated")
                + Count(score, "ScoreChanged")
                + Count(score, "NewBestReached")
                + Count(score.ComboTracker, "ComboChanged")
                + Count(_installer.Manager, "RunStarted")
                + Count(_installer.Manager, "RunEnded")
                + Count(_installer.Manager, "GameOverReady")
                + Count(_installer.Manager, "StateChanged");
        }

        /// <summary>
        /// Number of listeners of a field-like event, found by the name of its backing field.
        /// </summary>
        private static int Count(object owner, string eventName)
        {
            var field = owner.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, $"{owner.GetType().Name} has no event field {eventName}.");
            var handler = (Delegate)field.GetValue(owner);
            return handler == null ? 0 : handler.GetInvocationList().Length;
        }

        /// <summary>
        /// Reads a private field of a scene object.
        /// </summary>
        private static T Field<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, $"{owner.GetType().Name} has no field {name}.");
            return (T)field.GetValue(owner);
        }
    }
}
