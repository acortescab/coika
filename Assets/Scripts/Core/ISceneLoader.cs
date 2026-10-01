using System;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Coika.Core
{
    /// <summary>
    /// Single entry point for loading and unloading Addressable scenes.
    /// </summary>
    public interface ISceneLoader
    {
        Task<SceneInstance> LoadScene(AssetReference sceneReference, LoadSceneMode mode = LoadSceneMode.Single, Action<float> onProgress = null);
        Task<SceneInstance> LoadScene(string sceneKey, LoadSceneMode mode = LoadSceneMode.Single, Action<float> onProgress = null);
        Task UnloadScene(AssetReference sceneReference);
        Task UnloadScene(string sceneKey);
        bool IsSceneLoaded(string sceneKey);
    }
}
