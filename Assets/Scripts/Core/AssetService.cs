using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Coika.Core
{
    /// <summary>
    /// A service for loading assets via Unity's Addressable system with built-in retry logic and error handling.
    /// </summary>
    public class AssetService : IAssetService
    {
        private const int RETRY_COUNT = 3; // Number of retry attempts for loading an asset
        private const int RETRY_DELAY_MS = 500; // Delay between retry attempts in milliseconds

        /// <summary>
        /// Loads an asset identified by its AssetReference, with retry logic.
        /// </summary>
        /// <param name="assetReference"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="AssetLoadException"></exception>
        public async Task<T> LoadAsset<T>(AssetReference assetReference)
        {
            if (assetReference == null || !assetReference.RuntimeKeyIsValid())
                throw new ArgumentException("AssetReference is null or has no valid key.", nameof(assetReference));

            return await LoadWithRetry(() => Addressables.LoadAssetAsync<T>(assetReference), assetReference.RuntimeKey.ToString());
        }

        /// <summary>
        /// Loads an asset identified by a label string, with retry logic.
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="AssetLoadException"></exception>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="label"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<T> LoadAsset<T>(string label)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Label is null or empty.", nameof(label));

            return await LoadWithRetry(() => Addressables.LoadAssetAsync<T>(label), label);
        }

        /// <summary>
        /// Releases a previously loaded asset back to the Addressables system.
        /// </summary>
        /// <param name="objectToRelease"></param>
        public void ReleaseAsset(UnityEngine.Object objectToRelease)
        {
            if (objectToRelease != null)
                Addressables.Release(objectToRelease);
        }

        /// <summary>
        /// Preloads an asset identified by its AssetReference, with optional progress reporting and retry logic.
        /// </summary>
        /// <param name="assetReference"></param>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="AssetLoadException"></exception>
        public async Task PreloadAsset(AssetReference assetReference, Action<float> onProgress = null)
        {
            if (assetReference == null || !assetReference.RuntimeKeyIsValid())
                throw new ArgumentException("AssetReference is null or has no valid key.", nameof(assetReference));

            var key = assetReference.RuntimeKey.ToString();
            Exception lastError = null;

            for (int attempt = 1; attempt <= RETRY_COUNT; attempt++)
            {
                AsyncOperationHandle handle = default;
                try
                {
                    handle = Addressables.DownloadDependenciesAsync(assetReference.RuntimeKey);

                    while (!handle.IsDone)
                    {
                        onProgress?.Invoke(handle.PercentComplete);
                        await Task.Yield();
                    }

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        onProgress?.Invoke(1f);
                        Addressables.Release(handle);
                        return;
                    }

                    lastError = handle.OperationException;
                }
                catch (Exception e)
                {
                    lastError = e;
                }

                if (handle.IsValid())
                    Addressables.Release(handle);

                Debug.LogWarning($"Preload attempt {attempt}/{RETRY_COUNT} failed for {key}: {lastError?.Message}");
                await WaitBeforeRetry(attempt);
            }

            Debug.LogError($"Failed to preload asset: {key}");
            throw new AssetLoadException(key, lastError);
        }

        /// <summary>
        /// Preloads an asset identified by its label, with optional progress reporting and retry logic.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="onProgress"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="AssetLoadException"></exception>
        public async Task PreloadAsset(string label, Action<float> onProgress = null)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Label is null or empty.", nameof(label));

            Exception lastError = null;

            for (int attempt = 1; attempt <= RETRY_COUNT; attempt++)
            {
                AsyncOperationHandle handle = default;
                try
                {
                    handle = Addressables.DownloadDependenciesAsync(label);

                    while (!handle.IsDone)
                    {
                        onProgress?.Invoke(handle.PercentComplete);
                        await Task.Yield();
                    }

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        onProgress?.Invoke(1f);
                        Addressables.Release(handle);
                        return;
                    }

                    lastError = handle.OperationException;
                }
                catch (Exception e)
                {
                    lastError = e;
                }

                if (handle.IsValid())
                    Addressables.Release(handle);

                Debug.LogWarning($"Preload attempt {attempt}/{RETRY_COUNT} failed for {label}: {lastError?.Message}");
                await WaitBeforeRetry(attempt);
            }

            Debug.LogError($"Failed to preload asset: {label}");
            throw new AssetLoadException(label, lastError);
        }

        /// <summary>
        /// Loads an asset with retry logic in case of failure.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="startLoad"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="AssetLoadException"></exception>
        private async Task<T> LoadWithRetry<T>(Func<AsyncOperationHandle<T>> startLoad, string key)
        {
            Exception lastError = null;

            for (int attempt = 1; attempt <= RETRY_COUNT; attempt++)
            {
                AsyncOperationHandle<T> handle = default;
                try
                {
                    handle = startLoad();
                    await handle.Task;

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                        return handle.Result;

                    lastError = handle.OperationException;
                }
                catch (Exception e)
                {
                    lastError = e;
                }

                // A failed handle still holds resources until released.
                if (handle.IsValid())
                    Addressables.Release(handle);

                Debug.LogWarning($"Load attempt {attempt}/{RETRY_COUNT} failed for {key}: {lastError?.Message}");
                await WaitBeforeRetry(attempt);
            }

            Debug.LogError($"Failed to load asset: {key}");
            throw new AssetLoadException(key, lastError);
        }

        /// <summary>
        /// Waits for a specified duration before retrying an asset load operation, with linear backoff.
        /// </summary>
        /// <param name="attempt"></param>
        /// <returns></returns>
        private async Task WaitBeforeRetry(int attempt)
        {
            if (attempt >= RETRY_COUNT)
                return;

            await Task.Delay(RETRY_DELAY_MS * attempt);
        }
    }
}
