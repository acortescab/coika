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
        private const string AUDIO_VOICE_PATH = "Assets/Prefabs/Audio/AudioVoice.prefab";
        private const int MAX_WAIT_FRAMES = 6000;

        private SceneLoaderService _inner;
        private GameObject _host;
        private SimulationMode2D _originalSimulationMode;
        private string _sceneKey;
        private UnityEngine.Events.UnityAction<Scene, LoadSceneMode> _sceneLoaded;

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

            if (_sceneLoaded != null)
            {
                SceneManager.sceneLoaded -= _sceneLoaded;
                _sceneLoaded = null;
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
            var installer = CreateInstaller();

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
        /// Booting starts the audio engine with its 8 pooled voices; the gameplay music plays once the run starts
        /// (so the scene's sound bank loaded and was added), and unloading the scene stops it (the bank was removed
        /// before it was released).
        /// </summary>
        [UnityTest]
        public IEnumerator Boot_FromTheInstaller_StartsTheAudioAndTheSceneBankLivesWithTheScene()
        {
            var installer = CreateInstaller();

            // The host is inactive, so Start never connected the installer to the loaded scenes: do it as Start does.
            // The teardown disconnects it, because an object that never ran does not get OnDestroy.
            _sceneLoaded = (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction<Scene, LoadSceneMode>), installer, "HandleSceneLoaded", false);
            SceneManager.sceneLoaded += _sceneLoaded;

            var boot = installer.Boot();
            yield return new WaitUntil(() => boot.IsCompleted);
            Assert.IsFalse(boot.IsFaulted, boot.Exception?.ToString());

            var audio = installer.Audio as AudioManager;
            Assert.IsNotNull(audio, "Boot should have started the audio manager.");
            Assert.AreEqual(8, audio.VoiceCount);

            // The host is inactive, so the sources under the manager are inactive too and nothing really sounds: the
            // music is checked by what the manager asked for.
            for (var frame = 0; frame < MAX_WAIT_FRAMES && ReadField<MusicId?>(audio, "_currentMusic") == null; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(MusicId.Gameplay, ReadField<MusicId?>(audio, "_currentMusic"), "The gameplay music should start with the run.");
            Assert.AreEqual(1, ReadField<System.Collections.ICollection>(audio, "_banks").Count, "The scene should have added its sound bank.");

            var unload = _inner.UnloadScene(_sceneKey);
            yield return new WaitUntil(() => unload.IsCompleted);

            Assert.IsNull(ReadField<MusicId?>(audio, "_currentMusic"), "Unloading the scene should stop the music of its bank.");
            Assert.AreEqual(0, ReadField<System.Collections.ICollection>(audio, "_banks").Count, "The scene should have removed its sound bank.");
        }

        /// <summary>
        /// The installer hands its device tier to the Game scene before the scene builds: on a Low device the
        /// post-processing Volume of the scene is off once the run is playing (issue #39).
        /// </summary>
        [UnityTest]
        public IEnumerator Boot_OnALowDevice_DisablesThePostProcessingOfTheScene()
        {
            var installer = CreateInstaller();
            typeof(GameInstaller).GetProperty(nameof(GameInstaller.Quality)).GetSetMethod(true)
                .Invoke(installer, new object[] { new QualityTierService(new FakeSystemInfo(2, 8192)) });
            _sceneLoaded = (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction<Scene, LoadSceneMode>), installer, "HandleSceneLoaded", false);
            SceneManager.sceneLoaded += _sceneLoaded;

            var boot = installer.Boot();
            yield return new WaitUntil(() => boot.IsCompleted);
            Assert.IsFalse(boot.IsFaulted, boot.Exception?.ToString());

            var sceneInstaller = UnityEngine.Object.FindAnyObjectByType<GameSceneInstaller>();
            for (var frame = 0; frame < MAX_WAIT_FRAMES; frame++)
            {
                if (sceneInstaller.Manager != null && sceneInstaller.Manager.State == GameState.Playing)
                {
                    break;
                }

                yield return null;
            }

            Assert.AreEqual(GameState.Playing, sceneInstaller.Manager.State);
            var volume = ReadField<UnityEngine.Rendering.Volume>(sceneInstaller, "_postProcessing");
            Assert.IsNotNull(volume, "The Game scene needs its post-processing Volume wired to the installer.");
            Assert.IsFalse(volume.enabled, "A Low device should have the post-processing off.");
        }

        /// <summary>
        /// Reads a private field, for the state of the audio manager that has no public view.
        /// </summary>
        /// <typeparam name="T">Type of the field.</typeparam>
        /// <param name="owner">Object that owns the field.</param>
        /// <param name="name">Name of the field.</param>
        /// <returns>The value.</returns>
        private static T ReadField<T>(object owner, string name)
        {
            return (T)owner.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(owner);
        }

        /// <summary>
        /// Builds an installer on an inactive host, wired like the Boot scene, with the real asset service and a
        /// loader that loads additively.
        /// </summary>
        /// <returns>The installer; its host is destroyed by the teardown.</returns>
        private GameInstaller CreateInstaller()
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
            TestReflection.SetField(installer, "_audioVoice", new AssetReference(UnityEditor.AssetDatabase.AssetPathToGUID(AUDIO_VOICE_PATH)));
            typeof(GameInstaller).GetProperty(nameof(GameInstaller.Assets)).GetSetMethod(true).Invoke(installer, new object[] { new AssetService() });
            typeof(GameInstaller).GetProperty(nameof(GameInstaller.Scenes)).GetSetMethod(true).Invoke(installer, new object[] { new AdditiveSceneLoader(_inner) });
            return installer;
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
