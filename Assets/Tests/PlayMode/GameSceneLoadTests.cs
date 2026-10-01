using System.Collections;
using Coika.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Loads the Game scene through Addressables. Run with the Addressables Play Mode Script set to "Use Asset Database".
    /// </summary>
    public class GameSceneLoadTests
    {
        private const string GameSceneKey = "Assets/Scenes/GameScene.unity";

        /// <summary>
        /// The Game scene loads additively through the scene loader, is reported as loaded, and after unloading
        /// it is no longer reported as loaded.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadScene_GameScene_LoadsThenUnloads()
        {
            var loader = new SceneLoaderService();

            var load = loader.LoadScene(GameSceneKey, LoadSceneMode.Additive);
            yield return new WaitUntil(() => load.IsCompleted);

            Assert.IsFalse(load.IsFaulted, load.Exception?.ToString());
            Assert.IsTrue(loader.IsSceneLoaded(GameSceneKey));

            var unload = loader.UnloadScene(GameSceneKey);
            yield return new WaitUntil(() => unload.IsCompleted);

            Assert.IsFalse(loader.IsSceneLoaded(GameSceneKey));
        }
    }
}
