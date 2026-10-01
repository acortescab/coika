using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Coika.Core
{
    /// <summary>
    /// Addressables-backed implementation of <see cref="IAssetService"/>. Every operation is retried
    /// <see cref="RETRY_COUNT"/> times with a growing delay and fails with an <see cref="AssetLoadException"/>
    /// instead of leaving a failed handle or an endless wait behind (C-01).
    /// </summary>
    public class AssetService : IAssetService
    {
        private const int RETRY_COUNT = 3; // Number of attempts for an operation
        private const int RETRY_DELAY_MS = 500; // Base delay between attempts in milliseconds

        /// <summary>
        /// Loads an asset identified by an AssetReference, retrying on failure.
        /// </summary>
        /// <typeparam name="T">Type of the asset to load.</typeparam>
        /// <param name="assetReference">Reference to the asset. Must have a valid key.</param>
        /// <returns>The loaded asset. The caller must pass it to <see cref="ReleaseAsset"/> when done.</returns>
        /// <exception cref="ArgumentException">The reference is null or has no valid key.</exception>
        /// <exception cref="AssetLoadException">The load failed after all attempts.</exception>
        public async Task<T> LoadAsset<T>(AssetReference assetReference)
        {
            if (assetReference == null || !assetReference.RuntimeKeyIsValid())
                throw new ArgumentException("AssetReference is null or has no valid key.", nameof(assetReference));

            return await LoadWithRetry(() => Addressables.LoadAssetAsync<T>(assetReference), assetReference.RuntimeKey.ToString());
        }

        /// <summary>
        /// Loads a single asset identified by an Addressable key, address or label string, retrying on failure.
        /// </summary>
        /// <typeparam name="T">Type of the asset to load.</typeparam>
        /// <param name="label">Addressable key. When it is a label shared by several assets, only one is returned.</param>
        /// <returns>The loaded asset. The caller must pass it to <see cref="ReleaseAsset"/> when done.</returns>
        /// <exception cref="ArgumentException">The label is null or empty.</exception>
        /// <exception cref="AssetLoadException">The load failed after all attempts.</exception>
        public async Task<T> LoadAsset<T>(string label)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Label is null or empty.", nameof(label));

            return await LoadWithRetry(() => Addressables.LoadAssetAsync<T>(label), label);
        }

        /// <summary>
        /// Releases an asset previously returned by LoadAsset, decrementing its Addressables ref-count.
        /// </summary>
        /// <param name="objectToRelease">The loaded asset. A null or already destroyed object is ignored.</param>
        public void ReleaseAsset(UnityEngine.Object objectToRelease)
        {
            if (objectToRelease != null)
                Addressables.Release(objectToRelease);
        }

        /// <summary>
        /// Warms up the bundles of an asset (downloads or resolves its dependencies) without keeping anything in
        /// memory, so a later load does not hitch. There is nothing to release afterwards.
        /// </summary>
        /// <param name="assetReference">Reference to the asset to preload. Must have a valid key.</param>
        /// <param name="onProgress">Optional callback that receives values from 0 to 1.</param>
        /// <exception cref="ArgumentException">The reference is null or has no valid key.</exception>
        /// <exception cref="AssetLoadException">The preload failed after all attempts.</exception>
        public async Task PreloadAsset(AssetReference assetReference, Action<float> onProgress = null)
        {
            if (assetReference == null || !assetReference.RuntimeKeyIsValid())
                throw new ArgumentException("AssetReference is null or has no valid key.", nameof(assetReference));

            await PreloadWithRetry(assetReference.RuntimeKey, assetReference.RuntimeKey.ToString(), onProgress);
        }

        /// <summary>
        /// Warms up the bundles of every asset that has the given label, without keeping anything in memory.
        /// Prefer this over preloading assets one by one (C-01). There is nothing to release afterwards.
        /// </summary>
        /// <param name="label">Addressable label or key.</param>
        /// <param name="onProgress">Optional callback that receives values from 0 to 1.</param>
        /// <exception cref="ArgumentException">The label is null or empty.</exception>
        /// <exception cref="AssetLoadException">The preload failed after all attempts.</exception>
        public async Task PreloadAsset(string label, Action<float> onProgress = null)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Label is null or empty.", nameof(label));

            await PreloadWithRetry(label, label, onProgress);
        }

        /// <summary>
        /// Downloads the dependencies of a key, reporting progress and retrying on failure. The operation handle is
        /// always released, on success and on failure.
        /// </summary>
        /// <param name="key">Addressable key or label to download.</param>
        /// <param name="displayKey">Text used in logs and in the exception.</param>
        /// <param name="onProgress">Optional progress callback, values from 0 to 1.</param>
        /// <exception cref="AssetLoadException">The download failed after all attempts.</exception>
        private async Task PreloadWithRetry(object key, string displayKey, Action<float> onProgress)
        {
            Exception lastError = null;

            for (int attempt = 1; attempt <= RETRY_COUNT; attempt++)
            {
                AsyncOperationHandle handle = default;
                try
                {
                    handle = Addressables.DownloadDependenciesAsync(key);

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

                Debug.LogWarning($"Preload attempt {attempt}/{RETRY_COUNT} failed for {displayKey}: {lastError?.Message}");
                await WaitBeforeRetry(attempt);
            }

            Debug.LogError($"Failed to preload asset: {displayKey}");
            throw new AssetLoadException(displayKey, lastError);
        }

        /// <summary>
        /// Runs a load operation, retrying on failure. A failed attempt releases its handle before the next one
        /// starts, so no failed handle is left unobserved.
        /// </summary>
        /// <typeparam name="T">Type of the asset to load.</typeparam>
        /// <param name="startLoad">Starts a new load operation. Called once per attempt.</param>
        /// <param name="key">Text used in logs and in the exception.</param>
        /// <returns>The loaded asset; its handle stays alive until the caller releases the asset.</returns>
        /// <exception cref="AssetLoadException">The load failed after all attempts.</exception>
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
        /// Waits before the next attempt with a linear backoff. Does nothing after the last attempt.
        /// </summary>
        /// <param name="attempt">1-based number of the attempt that just failed.</param>
        private async Task WaitBeforeRetry(int attempt)
        {
            if (attempt >= RETRY_COUNT)
                return;

            await Task.Delay(RETRY_DELAY_MS * attempt);
        }
    }
}
