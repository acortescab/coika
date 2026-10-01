using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Coika.Core
{
    /// <summary>
    /// Single entry point for loading and unloading Addressable scenes. It tracks the scenes it loads so they
    /// can be unloaded and released later (C-01).
    /// </summary>
    public interface ISceneLoader
    {
        /// <summary>
        /// Loads a scene identified by an AssetReference.
        /// </summary>
        /// <param name="sceneReference">Reference to the scene. Must have a valid key.</param>
        /// <param name="mode">Single replaces the current scenes; Additive adds the scene on top.</param>
        /// <param name="onProgress">Optional callback that receives values from 0 to 1 while loading.</param>
        /// <returns>The loaded scene. If it was already loaded, the existing instance.</returns>
        /// <exception cref="ArgumentException">The reference is null or has no valid key.</exception>
        /// <exception cref="InvalidOperationException">The same scene is already being loaded.</exception>
        /// <exception cref="AssetLoadException">The load failed after all retries.</exception>
        Task<SceneInstance> LoadScene(AssetReference sceneReference, LoadSceneMode mode = LoadSceneMode.Single, Action<float> onProgress = null);

        /// <summary>
        /// Loads a scene identified by its Addressable key or address.
        /// </summary>
        /// <param name="sceneKey">Addressable key or address of the scene.</param>
        /// <param name="mode">Single replaces the current scenes; Additive adds the scene on top.</param>
        /// <param name="onProgress">Optional callback that receives values from 0 to 1 while loading.</param>
        /// <returns>The loaded scene. If it was already loaded, the existing instance.</returns>
        /// <exception cref="ArgumentException">The key is null or empty.</exception>
        /// <exception cref="InvalidOperationException">The same scene is already being loaded.</exception>
        /// <exception cref="AssetLoadException">The load failed after all retries.</exception>
        Task<SceneInstance> LoadScene(string sceneKey, LoadSceneMode mode = LoadSceneMode.Single, Action<float> onProgress = null);

        /// <summary>
        /// Unloads a scene previously loaded through this loader and releases its handle.
        /// </summary>
        /// <param name="sceneReference">Reference to the scene. Must have a valid key.</param>
        /// <exception cref="ArgumentException">The reference is null or has no valid key.</exception>
        Task UnloadScene(AssetReference sceneReference);

        /// <summary>
        /// Unloads a scene previously loaded through this loader and releases its handle.
        /// A scene that was not loaded through this loader is ignored with a warning.
        /// </summary>
        /// <param name="sceneKey">Addressable key or address of the scene.</param>
        /// <exception cref="ArgumentException">The key is null or empty.</exception>
        Task UnloadScene(string sceneKey);

        /// <summary>
        /// Whether a scene was loaded through this loader and is still loaded.
        /// </summary>
        /// <param name="sceneKey">Addressable key or address of the scene.</param>
        bool IsSceneLoaded(string sceneKey);
    }
}
