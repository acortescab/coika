using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Coika.Core
{
    /// <summary>
    /// Composition root. Lives in the Boot scene, creates the core services once, keeps them alive across scenes
    /// and loads the first Addressable scene. Other classes receive the services through Initialize(...), never by lookup.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameInstaller : MonoBehaviour
    {
        [SerializeField]
        private AssetReference _gameScene; // Reference to the initial game scene to load

        /// <summary>Service used to load and release assets. Available once Start has run.</summary>
        public IAssetService Assets { get; private set; }

        /// <summary>Service used to load and unload scenes. Available once Start has run.</summary>
        public ISceneLoader Scenes { get; private set; }

        /// <summary>Persisted save data. Loaded before the first scene.</summary>
        public SaveSystem Save { get; private set; }

        /// <summary>Observable user settings backed by <see cref="Save"/>.</summary>
        public SettingsService Settings { get; private set; }

        /// <summary>
        /// Keeps this object alive across scenes, creates the services and starts the boot flow.
        /// </summary>
        async void Start()
        {
            // DontDestroyOnLoad only works on root objects; Boot is replaced when the Game scene loads in Single mode.
            DontDestroyOnLoad(gameObject);

            Assets = new AssetService();
            Scenes = new SceneLoaderService();

            Save = new SaveSystem(new FileSaveStorage(Application.persistentDataPath));
            Save.Load();
            Settings = new SettingsService(Save);
            gameObject.AddComponent<SaveTriggers>().Initialize(Save);
            SceneManager.sceneLoaded += HandleSceneLoaded;

            await Boot();
        }

        /// <summary>
        /// Initializes Addressables and loads the game scene. Failures are logged and not rethrown, so the app
        /// never crashes on a failed boot.
        /// </summary>
        public async Task Boot()
        {
            try
            {
                await Addressables.InitializeAsync().Task;
                await Scenes.LoadScene(_gameScene);
            }
            catch (Exception e)
            {
                // TODO: show a recoverable error state with a retry button that calls Boot() again (constraints C-01).
                Debug.LogError($"Boot failed: {e.Message}");
            }
        }

        /// <summary>
        /// Stops listening for loaded scenes.
        /// </summary>
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>
        /// Gives the save system and the settings to the roots of a scene that ask for them. Unity raises
        /// sceneLoaded after the Awake of the scene and before any Start, so both always arrive before the scene
        /// builds its objects.
        /// </summary>
        /// <param name="scene">The scene that was just loaded.</param>
        /// <param name="mode">How the scene was loaded.</param>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is ISaveConsumer saveConsumer)
                    {
                        saveConsumer.UseSave(Save);
                    }

                    if (behaviour is ISettingsConsumer settingsConsumer)
                    {
                        settingsConsumer.UseSettings(Settings);
                    }
                }
            }
        }
    }
}
