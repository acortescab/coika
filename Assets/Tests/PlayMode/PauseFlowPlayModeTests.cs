using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Coika.Core;
using Coika.Data;
using Coika.Fx;
using Coika.Gameplay;
using Coika.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Plays the real Game scene through the pause flow (issue #35): the freeze, a pause during slow-mo, the
    /// auto-pause and its save, the Back button, Restart with its confirmation, and the tap on Resume that must not
    /// drop a piece. Run with the Addressables Play Mode Script set to "Use Asset Database".
    /// </summary>
    public class PauseFlowPlayModeTests
    {
        private const string SCENE_KEY = "Assets/Scenes/GameScene.unity";
        private const float LOAD_TIMEOUT = 20f;
        private const float FREEZE_SECONDS = 0.4f;
        private const float POSITION_TOLERANCE = 0.0001f;

        private SceneLoaderService _loader;
        private GameSceneInstaller _installer;
        private GameManager _manager;

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

            var until = Time.realtimeSinceStartup + LOAD_TIMEOUT;
            yield return new WaitUntil(() => (_installer.Manager != null && _installer.Manager.State == GameState.Playing) || Time.realtimeSinceStartup > until);
            Assert.IsNotNull(_installer.Manager, "The game did not finish loading.");
            _manager = _installer.Manager;
        }

        /// <summary>
        /// Unloads the scene and gives the time and the physics back.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_loader.IsSceneLoaded(SCENE_KEY))
            {
                var unload = _loader.UnloadScene(SCENE_KEY);
                yield return new WaitUntil(() => unload.IsCompleted);
            }

            Time.timeScale = 1f;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        /// <summary>
        /// Pausing stops the time and the pieces; resuming continues from the same positions at normal speed.
        /// </summary>
        [UnityTest]
        public IEnumerator Pause_WhileThePiecesFall_FreezesThemUntilResumed()
        {
            var factory = Field<PieceFactory>(_installer, "_factory");
            var tiers = Field<IReadOnlyList<TierDefinition>>(_installer, "_tiers");
            var piece = factory.Create(tiers[0], new Vector2(0f, 3f), Vector2.zero);
            yield return new WaitForSeconds(0.1f);

            _manager.Pause();
            yield return null;
            var frozenAt = piece.transform.position;
            yield return new WaitForSecondsRealtime(FREEZE_SECONDS);

            Assert.AreEqual(0f, Time.timeScale);
            Assert.AreEqual(GameState.Paused, _manager.State);
            Assert.Less(Vector3.Distance(frozenAt, piece.transform.position), POSITION_TOLERANCE);

            _manager.Resume();
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(1f, Time.timeScale);
            Assert.Greater(Vector3.Distance(frozenAt, piece.transform.position), POSITION_TOLERANCE, "The piece keeps falling after the resume.");
        }

        /// <summary>
        /// A slow-mo that was running when the game paused is gone after the resume: the scale is 1.
        /// </summary>
        [UnityTest]
        public IEnumerator Pause_DuringSlowMo_EndsWithTimeScaleOneAfterResume()
        {
            var timeScale = Field<TimeScaleOwner>(_installer, "_timeScale");
            Assert.IsNotNull(timeScale, "The scene needs a feedback config for the slow-mo.");
            timeScale.Begin(0.3f, 5f);
            yield return null;
            Assert.Less(Time.timeScale, 1f);

            _manager.Pause();
            yield return null;
            Assert.AreEqual(0f, Time.timeScale);

            _manager.Resume();
            yield return null;

            Assert.AreEqual(1f, Time.timeScale);
        }

        /// <summary>
        /// An interruption (the app goes to the background) pauses the run, writes the save, and never resumes it.
        /// </summary>
        [UnityTest]
        public IEnumerator Interruption_WhilePlaying_PausesAndSavesAndStaysPaused()
        {
            var storage = new TestSaveStorage();
            var save = new SaveSystem(storage);
            save.Load();
            _installer.UseSave(save);

            _installer.PauseForInterruption();
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.AreEqual(GameState.Paused, _manager.State);
            Assert.IsTrue(storage.TryRead(out var json) && !string.IsNullOrEmpty(json), "The save file was written.");
            Assert.IsTrue(FindPauseView().gameObject.activeSelf, "The pause menu is shown.");
        }

        /// <summary>
        /// An interruption after the game is over changes nothing.
        /// </summary>
        [Test]
        public void Interruption_WhileGameOver_DoesNotPause()
        {
            _manager.EndRun();

            _installer.PauseForInterruption();

            Assert.AreEqual(GameState.GameOver, _manager.State);
        }

        /// <summary>
        /// Back opens the pause menu while playing, closes a confirmation dialog first, and resumes from the menu.
        /// </summary>
        [UnityTest]
        public IEnumerator Back_InPlayAndInPause_OpensThenClosesThePanelsInOrder()
        {
            PressBack();
            yield return null;
            Assert.AreEqual(GameState.Paused, _manager.State);

            ClickRestart();
            Assert.IsTrue(FindConfirmView().gameObject.activeSelf);

            PressBack();
            Assert.IsFalse(FindConfirmView().gameObject.activeSelf, "Back cancels the dialog first.");
            Assert.AreEqual(GameState.Paused, _manager.State);

            PressBack();
            yield return null;

            Assert.AreEqual(GameState.Playing, _manager.State);
            Assert.IsFalse(FindPauseView().gameObject.activeSelf);
            Assert.AreEqual(1f, Time.timeScale);
        }

        /// <summary>
        /// Restart asks first, and confirming it starts a fresh run with the same number of pieces and listeners.
        /// </summary>
        [UnityTest]
        public IEnumerator Restart_WhenConfirmed_StartsAFreshRunWithTheSameListeners()
        {
            var factory = Field<PieceFactory>(_installer, "_factory");
            var tiers = Field<IReadOnlyList<TierDefinition>>(_installer, "_tiers");
            var baselinePieces = factory.ActivePieces.Count;
            var baselineListeners = CountListeners();
            factory.Create(tiers[0], new Vector2(0f, 1f), Vector2.zero);
            factory.Create(tiers[1], new Vector2(1f, 1f), Vector2.zero);

            _manager.Pause();
            yield return null;
            ClickRestart();
            Assert.AreEqual(GameState.Paused, _manager.State, "Nothing restarts before the confirmation.");

            ClickConfirm();
            yield return null;

            Assert.AreEqual(GameState.Playing, _manager.State);
            Assert.AreEqual(baselinePieces, factory.ActivePieces.Count);
            Assert.AreEqual(baselineListeners, CountListeners());
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(FindPauseView().gameObject.activeSelf);
        }

        /// <summary>
        /// A press that comes right after the Resume tap, inside the grace period, drops nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator Resume_ThenAPressInsideTheGrace_DropsNoPiece()
        {
            var controller = Field<DropController>(_installer, "_dropController");
            var drops = 0;
            controller.PieceDropped += _ => drops++;

            _manager.Pause();
            yield return null;
            ClickResume();

            var state = Field<object>(Field<PointerInputReader>(_installer, "_input"), "_state");
            Invoke(state, "PointerPressed", false, false);
            Invoke(state, "PointerReleased", true);
            yield return null;

            Assert.AreEqual(GameState.Playing, _manager.State);
            Assert.AreEqual(0, drops, "The tap on Resume and a press right after it drop nothing.");
        }

        /// <summary>
        /// Reset progress, reached through Pause and Settings, asks first; confirming it erases the saved bests and
        /// totals, keeps the settings, writes the save and shows the new best on the HUD.
        /// </summary>
        [UnityTest]
        public IEnumerator ResetProgress_WhenConfirmedFromSettings_ErasesProgressKeepsSettingsAndRefreshesTheHud()
        {
            var storage = new TestSaveStorage();
            var save = new SaveSystem(storage);
            save.Load();
            var settings = new SettingsService(save);
            settings.Music = 0.2f;
            save.Data.bestScore.classic = 500;
            _installer.UseSave(save);
            _installer.UseSettings(settings);
            var score = Field<ScoreSystem>(_installer, "_score");
            score.BestScore = 500;
            Field<HudPresenter>(_installer, "_hudPresenter").Refresh();
            var bestText = Field<TMPro.TMP_Text>(Field<HudView>(_installer, "_hud"), "_bestText");
            var shownBefore = UiTestViews.Shown(bestText);

            _manager.Pause();
            yield return null;
            Field<UnityEngine.UI.Button>(FindPauseView(), "_settingsButton").onClick.Invoke();
            Assert.IsTrue(FindSettingsView().gameObject.activeSelf, "The Settings screen opens over the pause menu.");
            Field<UnityEngine.UI.Button>(FindSettingsView(), "_resetButton").onClick.Invoke();
            Assert.AreEqual(500, save.Data.bestScore.classic, "Nothing is erased before the confirmation.");

            ClickConfirm();
            yield return null;

            Assert.AreEqual(0, save.Data.bestScore.classic);
            Assert.AreEqual(0, score.BestScore);
            Assert.AreEqual(0.2f, settings.Music, 0.0001f);
            Assert.IsTrue(storage.TryRead(out var json) && json.Contains("\"bestScore\""), "The save is written at once.");
            Assert.AreNotEqual(shownBefore, UiTestViews.Shown(bestText), "The HUD shows the new best.");
        }

        /// <summary>
        /// Back from the Settings screen returns to the pause menu, and a second Back resumes the run.
        /// </summary>
        [UnityTest]
        public IEnumerator Back_InSettings_ClosesTheScreenThenResumes()
        {
            _manager.Pause();
            yield return null;
            Field<UnityEngine.UI.Button>(FindPauseView(), "_settingsButton").onClick.Invoke();

            PressBack();
            Assert.IsFalse(FindSettingsView().gameObject.activeSelf);
            Assert.AreEqual(GameState.Paused, _manager.State);

            PressBack();
            yield return null;

            Assert.AreEqual(GameState.Playing, _manager.State);
        }

        /// <summary>
        /// Finds the Settings view of the scene, active or not.
        /// </summary>
        private static SettingsView FindSettingsView()
        {
            return UnityEngine.Object.FindAnyObjectByType<SettingsView>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Finds the pause view of the scene, active or not.
        /// </summary>
        private static PauseView FindPauseView()
        {
            return UnityEngine.Object.FindAnyObjectByType<PauseView>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Finds the confirmation view of the scene, active or not.
        /// </summary>
        private static ConfirmView FindConfirmView()
        {
            return UnityEngine.Object.FindAnyObjectByType<ConfirmView>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// Clicks the Resume button of the pause view.
        /// </summary>
        private static void ClickResume()
        {
            Field<UnityEngine.UI.Button>(FindPauseView(), "_resumeButton").onClick.Invoke();
        }

        /// <summary>
        /// Clicks the Restart button of the pause view.
        /// </summary>
        private static void ClickRestart()
        {
            Field<UnityEngine.UI.Button>(FindPauseView(), "_restartButton").onClick.Invoke();
        }

        /// <summary>
        /// Clicks the Confirm button of the dialog.
        /// </summary>
        private static void ClickConfirm()
        {
            Field<UnityEngine.UI.Button>(FindConfirmView(), "_confirmButton").onClick.Invoke();
        }

        /// <summary>
        /// Raises the Back key on the input state, as the Escape key or the Android Back button does.
        /// </summary>
        private void PressBack()
        {
            var state = Field<object>(Field<PointerInputReader>(_installer, "_input"), "_state");
            Invoke(state, "BackKeyPressed");
        }

        /// <summary>
        /// Counts the listeners of the events the pause flow and the run connect.
        /// </summary>
        private int CountListeners()
        {
            var presenter = Field<PausePresenter>(_installer, "_pausePresenter");
            return Count(_manager, "StateChanged")
                + Count(_manager, "RunStarted")
                + Count(presenter, "ResumeRequested")
                + Count(presenter, "Confirmed")
                + Count(Field<DropController>(_installer, "_dropController"), "PieceDropped")
                + Count(FindPauseView(), "Clicked")
                + Count(FindConfirmView(), "Answered");
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
        /// Calls a method of an object by name, public or not.
        /// </summary>
        private static void Invoke(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no method {method}.");
            info.Invoke(target, args);
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
