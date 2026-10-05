using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Coika.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// What <see cref="AssetService"/> and <see cref="SceneLoaderService"/> do with bad arguments and with scenes they
    /// never loaded: they fail with a clear exception, or warn, and never reach
    /// Addressables, so these tests need no bundles and no Play Mode.
    /// </summary>
    public class AssetServicesArgumentTests
    {
        /// <summary>
        /// Loading, or preloading, with a null reference throws an <see cref="ArgumentException"/> at once.
        /// </summary>
        [Test]
        public void AssetService_WithANullReference_ThrowsArgumentException()
        {
            var service = new AssetService();

            Assert.ThrowsAsync<ArgumentException>(async () => await service.LoadAsset<GameObject>((AssetReference)null));
            Assert.ThrowsAsync<ArgumentException>(async () => await service.PreloadAsset((AssetReference)null));
        }

        /// <summary>
        /// Loading, or preloading, with an empty key throws an <see cref="ArgumentException"/>.
        /// </summary>
        [Test]
        public void AssetService_WithAnEmptyKey_ThrowsArgumentException()
        {
            var service = new AssetService();

            Assert.ThrowsAsync<ArgumentException>(async () => await service.LoadAsset<GameObject>(string.Empty));
            Assert.ThrowsAsync<ArgumentException>(async () => await service.PreloadAsset(string.Empty));
        }

        /// <summary>
        /// Loading every asset of a label with an empty label throws an <see cref="ArgumentException"/>, and releasing
        /// a null list does nothing (issue #29).
        /// </summary>
        [Test]
        public void AssetService_LoadAssetsWithAnEmptyLabel_ThrowsAndReleasingNullIsIgnored()
        {
            var service = new AssetService();

            Assert.ThrowsAsync<ArgumentException>(async () => await service.LoadAssets<AudioClip>(string.Empty));
            Assert.DoesNotThrow(() => service.ReleaseAssets<AudioClip>(null));
        }

        /// <summary>
        /// Releasing a null asset does nothing, so a caller can always release in a cleanup path.
        /// </summary>
        [Test]
        public void AssetService_ReleasingNull_DoesNothing()
        {
            Assert.DoesNotThrow(() => new AssetService().ReleaseAsset(null));
        }

        /// <summary>
        /// Loading, or unloading, a scene with a null reference throws an <see cref="ArgumentException"/>.
        /// </summary>
        [Test]
        public void SceneLoaderService_WithANullReference_ThrowsArgumentException()
        {
            var loader = new SceneLoaderService();

            Assert.Throws<ArgumentException>(() => loader.LoadScene((AssetReference)null));
            Assert.Throws<ArgumentException>(() => loader.UnloadScene((AssetReference)null));
        }

        /// <summary>
        /// Loading, or unloading, a scene with an empty key faults the task with an <see cref="ArgumentException"/>.
        /// </summary>
        [Test]
        public void SceneLoaderService_WithAnEmptyKey_ThrowsArgumentException()
        {
            var loader = new SceneLoaderService();

            Assert.ThrowsAsync<ArgumentException>(async () => await loader.LoadScene(string.Empty));
            Assert.ThrowsAsync<ArgumentException>(async () => await loader.UnloadScene(string.Empty));
        }

        /// <summary>
        /// A scene the loader never loaded is not reported as loaded, and unloading it only logs a warning.
        /// </summary>
        [Test]
        public async Task SceneLoaderService_WithAnUnknownScene_IsNotLoadedAndUnloadingWarns()
        {
            var loader = new SceneLoaderService();
            LogAssert.Expect(LogType.Warning, new Regex("Cannot unload scene that was not loaded"));

            await loader.UnloadScene("scene-that-was-never-loaded");

            Assert.IsFalse(loader.IsSceneLoaded("scene-that-was-never-loaded"));
        }
    }
}
