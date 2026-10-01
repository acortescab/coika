using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Coika.Core
{
    /// <summary>
    /// Loads and unloads Addressable scenes with progress reporting, retry logic and error handling.
    /// Keeps the handle of every loaded scene so it can be released when the scene is unloaded.
    /// </summary>
    public class SceneLoaderService : ISceneLoader
    {
        private const int RETRY_COUNT = 3; // Number of attempts for loading a scene
        private const int RETRY_DELAY_MS = 500; // Base delay between attempts in milliseconds

        private readonly Dictionary<string, AsyncOperationHandle<SceneInstance>> _loadedScenes = new();
        private readonly HashSet<string> _loadingScenes = new();

        /// <summary>
        /// Loads a scene identified by its AssetReference.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AssetLoadException"></exception>
        public Task<SceneInstance> LoadScene(AssetReference sceneReference, LoadSceneMode mode = LoadSceneMode.Single, Action<float> onProgress = null)
        {
            if (sceneReference == null || !sceneReference.RuntimeKeyIsValid())
                throw new ArgumentException("AssetReference is null or has no valid key.", nameof(sceneReference));

            return LoadScene(sceneReference.RuntimeKey.ToString(), mode, onProgress);
        }

        /// <summary>
        /// Loads a scene identified by its Addressable key or address, with retry logic.
        /// If the scene is already loaded, returns the existing instance.
        /// </summary>
        /// <param name="sceneKey"></param>
        /// <param name="mode">Single replaces the current scenes; Additive adds the scene on top.</param>
        /// <param name="onProgress">Receives values from 0 to 1 while the scene loads.</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException">The same scene is already being loaded.</exception>
        /// <exception cref="AssetLoadException">The scene failed to load after all attempts.</exception>
        public async Task<SceneInstance> LoadScene(string sceneKey, LoadSceneMode mode = LoadSceneMode.Single, Action<float> onProgress = null)
        {
            if (string.IsNullOrEmpty(sceneKey))
                throw new ArgumentException("Scene key is null or empty.", nameof(sceneKey));

            if (_loadedScenes.TryGetValue(sceneKey, out var existing) && existing.IsValid())
                return existing.Result;

            if (!_loadingScenes.Add(sceneKey))
                throw new InvalidOperationException($"Scene is already being loaded: {sceneKey}");

            try
            {
                Exception lastError = null;

                for (int attempt = 1; attempt <= RETRY_COUNT; attempt++)
                {
                    AsyncOperationHandle<SceneInstance> handle = default;
                    try
                    {
                        handle = Addressables.LoadSceneAsync(sceneKey, mode);

                        while (!handle.IsDone)
                        {
                            onProgress?.Invoke(handle.PercentComplete);
                            await Task.Yield();
                        }

                        if (handle.Status == AsyncOperationStatus.Succeeded)
                        {
                            onProgress?.Invoke(1f);

                            if (mode == LoadSceneMode.Single)
                                ReleaseReplacedScenes();

                            _loadedScenes[sceneKey] = handle;
                            return handle.Result;
                        }

                        lastError = handle.OperationException;
                    }
                    catch (Exception e)
                    {
                        lastError = e;
                    }

                    // A failed handle still holds resources until released.
                    if (handle.IsValid())
                        Addressables.Release(handle);

                    Debug.LogWarning($"Scene load attempt {attempt}/{RETRY_COUNT} failed for {sceneKey}: {lastError?.Message}");
                    await WaitBeforeRetry(attempt);
                }

                Debug.LogError($"Failed to load scene: {sceneKey}");
                throw new AssetLoadException(sceneKey, lastError);
            }
            finally
            {
                _loadingScenes.Remove(sceneKey);
            }
        }

        /// <summary>
        /// Unloads a scene identified by its AssetReference.
        /// </summary>
        public Task UnloadScene(AssetReference sceneReference)
        {
            if (sceneReference == null || !sceneReference.RuntimeKeyIsValid())
                throw new ArgumentException("AssetReference is null or has no valid key.", nameof(sceneReference));

            return UnloadScene(sceneReference.RuntimeKey.ToString());
        }

        /// <summary>
        /// Unloads a scene previously loaded through this service and releases its handle.
        /// Logs a warning and does nothing if the scene was not loaded by this service.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public async Task UnloadScene(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey))
                throw new ArgumentException("Scene key is null or empty.", nameof(sceneKey));

            if (!_loadedScenes.TryGetValue(sceneKey, out var handle))
            {
                Debug.LogWarning($"Cannot unload scene that was not loaded by SceneLoaderService: {sceneKey}");
                return;
            }

            _loadedScenes.Remove(sceneKey);

            if (!handle.IsValid())
                return;

            try
            {
                var unloadHandle = Addressables.UnloadSceneAsync(handle);
                await unloadHandle.Task;

                if (unloadHandle.Status != AsyncOperationStatus.Succeeded)
                    Debug.LogError($"Failed to unload scene: {sceneKey}. {unloadHandle.OperationException?.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to unload scene: {sceneKey}. {e.Message}");
            }
        }

        /// <summary>
        /// Whether the scene was loaded through this service and is still loaded.
        /// </summary>
        public bool IsSceneLoaded(string sceneKey)
        {
            return _loadedScenes.TryGetValue(sceneKey, out var handle) && handle.IsValid();
        }

        // A Single load makes Unity unload every other scene; drop their handles so they don't leak.
        private void ReleaseReplacedScenes()
        {
            foreach (var handle in _loadedScenes.Values)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }

            _loadedScenes.Clear();
        }

        // Linear backoff between attempts; skipped after the last one.
        private async Task WaitBeforeRetry(int attempt)
        {
            if (attempt >= RETRY_COUNT)
                return;

            await Task.Delay(RETRY_DELAY_MS * attempt);
        }
    }
}
