#if UNITY_EDITOR
using System;
using System.Collections;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Gameplay;
using Coika.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Boots the game the way the Boot scene does (C-01): the <see cref="GameInstaller"/> initializes Addressables and
    /// loads the Game scene through the scene loader, and the run reaches <see cref="GameState.Playing"/>. The scene
    /// is loaded additively through a thin wrapper, because the real Boot load is in Single mode and would replace
    /// the scene of the test runner. Run with the Addressables Play Mode Script set to "Use Asset Database" (the
    /// default of the project); the Editor is needed to look up the GUID of the scene.
    /// </summary>
    public class BootToGamePlayModeTests
    {
        private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";
        private const int MAX_WAIT_FRAMES = 6000;

        private SceneLoaderService _inner;
        private GameObject _host;
        private SimulationMode2D _originalSimulationMode;
        private string _sceneKey;

        /// <summary>
        /// Unloads the Game scene, destroys the installer host and gives the physics back.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_inner != null && _sceneKey != null && _inner.IsSceneLoaded(_sceneKey))
            {
                var unload = _inner.UnloadScene(_sceneKey);
                yield return new WaitUntil(() => unload.IsCompleted);
            }

            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
            }

            Physics2D.simulationMode = _originalSimulationMode;
        }

        /// <summary>
        /// Calling Boot on the installer loads the Game scene through the scene loader, the scene installer composes
        /// the game, and the first run is playing.
        /// </summary>
        [UnityTest]
        public IEnumerator Boot_FromTheInstaller_LoadsTheGameSceneAndReachesPlaying()
        {
            _originalSimulationMode = Physics2D.simulationMode;
            _sceneKey = UnityEditor.AssetDatabase.AssetPathToGUID(GAME_SCENE_PATH);
            Assert.IsNotEmpty(_sceneKey, "The Game scene was not found.");

            _inner = new SceneLoaderService();

            // Inactive, so Start does not boot by itself with the real, Single mode loader.
            _host = new GameObject("BootHost");
            _host.SetActive(false);
            var installer = _host.AddComponent<GameInstaller>();
            TestReflection.SetField(installer, "_gameScene", new AssetReference(_sceneKey));
            typeof(GameInstaller).GetProperty(nameof(GameInstaller.Scenes)).GetSetMethod(true).Invoke(installer, new object[] { new AdditiveSceneLoader(_inner) });

            var boot = installer.Boot();
            yield return new WaitUntil(() => boot.IsCompleted);
            Assert.IsFalse(boot.IsFaulted, boot.Exception?.ToString());
            Assert.IsTrue(_inner.IsSceneLoaded(_sceneKey), "Boot should have loaded the Game scene through the scene loader.");

            var sceneInstaller = UnityEngine.Object.FindAnyObjectByType<GameSceneInstaller>();
            Assert.IsNotNull(sceneInstaller, "The Game scene needs a GameSceneInstaller.");

            for (var frame = 0; frame < MAX_WAIT_FRAMES; frame++)
            {
                if (sceneInstaller.Manager != null && sceneInstaller.Manager.State == GameState.Playing)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(sceneInstaller.Manager, "The scene installer never composed the game.");
            Assert.AreEqual(GameState.Playing, sceneInstaller.Manager.State);
        }


        /// <summary>
        /// Scene loader that forwards to a real <see cref="SceneLoaderService"/> but always loads additively, so the
        /// Boot flow can be tested without replacing the scene of the test runner.
        /// </summary>
        private sealed class AdditiveSceneLoader : ISceneLoader
        {
            private readonly SceneLoaderService _inner;

            /// <summary>
            /// Creates the wrapper.
            /// </summary>
            /// <param name="inner">The loader that does the work.</param>
            public AdditiveSceneLoader(SceneLoaderService inner)
            {
                _inner = inner;
            }

            /// <inheritdoc />
            public Task<SceneInstance> LoadScene(AssetReference sceneReference, LoadSceneMode mode = LoadSceneMode.Single, Action<float> onProgress = null)
            {
                return _inner.LoadScene(sceneReference, LoadSceneMode.Additive, onProgress);
            }

            /// <inheritdoc />
            public Task<SceneInstance> LoadScene(string sceneKey, LoadSceneMode mode = LoadSceneMode.Single, Action<float> onProgress = null)
            {
                return _inner.LoadScene(sceneKey, LoadSceneMode.Additive, onProgress);
            }

            /// <inheritdoc />
            public Task UnloadScene(AssetReference sceneReference)
            {
                return _inner.UnloadScene(sceneReference);
            }

            /// <inheritdoc />
            public Task UnloadScene(string sceneKey)
            {
                return _inner.UnloadScene(sceneKey);
            }

            /// <inheritdoc />
            public bool IsSceneLoaded(string sceneKey)
            {
                return _inner.IsSceneLoaded(sceneKey);
            }
        }
    }
}
#endif
