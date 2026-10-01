using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Coika.Core
{
    /// <summary>
    /// Single entry point for asset loading. Gameplay and UI depend on this interface, never on Addressables directly.
    /// Every successful load must be matched by a call to <see cref="ReleaseAsset"/> (C-01).
    /// </summary>
    public interface IAssetService
    {
        /// <summary>
        /// Loads an asset identified by an AssetReference.
        /// </summary>
        /// <typeparam name="T">Type of the asset to load.</typeparam>
        /// <param name="assetReference">Reference to the asset. Must have a valid key.</param>
        /// <returns>The loaded asset, to be released with <see cref="ReleaseAsset"/>.</returns>
        /// <exception cref="ArgumentException">The reference is null or has no valid key.</exception>
        /// <exception cref="AssetLoadException">The load failed after all retries.</exception>
        Task<T> LoadAsset<T>(AssetReference assetReference);

        /// <summary>
        /// Loads a single asset identified by an Addressable key, address or label.
        /// </summary>
        /// <typeparam name="T">Type of the asset to load.</typeparam>
        /// <param name="label">Addressable key, address or label.</param>
        /// <returns>The loaded asset, to be released with <see cref="ReleaseAsset"/>.</returns>
        /// <exception cref="ArgumentException">The label is null or empty.</exception>
        /// <exception cref="AssetLoadException">The load failed after all retries.</exception>
        Task<T> LoadAsset<T>(string label);

        /// <summary>
        /// Releases an asset previously returned by LoadAsset.
        /// </summary>
        /// <param name="objectToRelease">The loaded asset. Null is ignored.</param>
        void ReleaseAsset(UnityEngine.Object objectToRelease);

        /// <summary>
        /// Warms up the bundles of an asset without keeping it in memory. Nothing needs to be released.
        /// </summary>
        /// <param name="assetReference">Reference to the asset to preload. Must have a valid key.</param>
        /// <param name="onProgress">Optional callback that receives values from 0 to 1.</param>
        /// <exception cref="ArgumentException">The reference is null or has no valid key.</exception>
        /// <exception cref="AssetLoadException">The preload failed after all retries.</exception>
        Task PreloadAsset(AssetReference assetReference, Action<float> onProgress = null);

        /// <summary>
        /// Warms up the bundles of every asset with the given label without keeping them in memory.
        /// Nothing needs to be released.
        /// </summary>
        /// <param name="label">Addressable label or key.</param>
        /// <param name="onProgress">Optional callback that receives values from 0 to 1.</param>
        /// <exception cref="ArgumentException">The label is null or empty.</exception>
        /// <exception cref="AssetLoadException">The preload failed after all retries.</exception>
        Task PreloadAsset(string label, Action<float> onProgress = null);
    }
}
