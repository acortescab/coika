using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;

namespace Coika.Core
{
    /// <summary>
    /// Single entry point for asset loading. Gameplay and UI depend on this interface, never on Addressables directly.
    /// </summary>
    public interface IAssetService
    {
        Task<T> LoadAsset<T>(AssetReference assetReference);
        Task<T> LoadAsset<T>(string label);
        void ReleaseAsset(UnityEngine.Object objectToRelease);
        Task PreloadAsset(AssetReference assetReference, Action<float> onProgress = null);
        Task PreloadAsset(string label, Action<float> onProgress = null);
    }
}
