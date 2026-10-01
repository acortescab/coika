using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

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

        /// <summary>
        /// Keeps this object alive across scenes, creates the services and starts the boot flow.
        /// </summary>
        async void Start()
        {
            // DontDestroyOnLoad only works on root objects; Boot is replaced when the Game scene loads in Single mode.
            DontDestroyOnLoad(gameObject);

            Assets = new AssetService();
            Scenes = new SceneLoaderService();

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
    }
}
