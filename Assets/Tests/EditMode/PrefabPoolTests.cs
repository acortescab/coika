using System;
using System.Collections.Generic;
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
    /// Checks the generic <see cref="PrefabPool{T}"/> on its own, with a fake asset service and a plain
    /// BoxCollider2D as the pooled component, so any system can rely on it.
    /// </summary>
    public class PrefabPoolTests
    {
        private const int PREWARM_COUNT = 3;

        private readonly List<BoxCollider2D> _released = new();
        private FakeAssetService _assets;
        private Transform _container;
        private PrefabPool<BoxCollider2D> _pool;
        private bool _providePrefabWithoutComponent;

        /// <summary>
        /// Creates the fake service, the container and a pool that records the items it takes back.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            // NUnit reuses the fixture instance between tests, so the state they share is reset here.
            _providePrefabWithoutComponent = false;
            _released.Clear();

            _assets = new FakeAssetService { Provider = ProvideAsset };
            _container = new GameObject("PoolContainer").transform;
            _pool = CreatePool(PREWARM_COUNT);
        }

        /// <summary>
        /// Disposes the pool and destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _pool.Dispose();
            _assets.Cleanup();
            UnityEngine.Object.DestroyImmediate(_container.gameObject);
        }

        /// <summary>
        /// Pre-warming builds the requested number of disabled instances under the container and loads the prefab once.
        /// </summary>
        [Test]
        public async Task PrewarmAsync_Always_BuildsDisabledInstancesUnderTheContainer()
        {
            await _pool.PrewarmAsync();

            Assert.AreEqual(PREWARM_COUNT, _container.childCount);
            Assert.AreEqual(PREWARM_COUNT, _pool.PooledCount);
            Assert.AreEqual(0, _pool.Active.Count);
            for (int i = 0; i < _container.childCount; i++)
                Assert.IsFalse(_container.GetChild(i).gameObject.activeSelf);

            Assert.AreEqual(1, _assets.LoadCount, "The prefab loads once.");
        }

        /// <summary>
        /// Getting an item places it, enables it and lists it as active.
        /// </summary>
        [Test]
        public async Task Get_AfterPrewarm_PlacesAndEnablesTheItem()
        {
            await _pool.PrewarmAsync();
            var rotation = Quaternion.Euler(0f, 0f, 45f);

            var item = _pool.Get(new Vector3(1f, 2f, 0f), rotation);

            Assert.IsTrue(item.gameObject.activeSelf);
            Assert.AreEqual(new Vector3(1f, 2f, 0f), item.transform.position);
            Assert.AreEqual(rotation, item.transform.rotation);
            CollectionAssert.Contains(_pool.Active, item);
            Assert.AreEqual(PREWARM_COUNT - 1, _pool.PooledCount);
        }

        /// <summary>
        /// Releasing an item runs the cleanup, disables it and returns it to the pool, and the next get reuses it.
        /// </summary>
        [Test]
        public async Task Release_ThenGet_RunsTheCleanupAndReusesTheSameInstance()
        {
            await _pool.PrewarmAsync();
            var first = _pool.Get(Vector3.zero, Quaternion.identity);

            _pool.Release(first);

            CollectionAssert.AreEqual(new[] { first }, _released, "The cleanup ran with the released item.");
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(PREWARM_COUNT, _pool.PooledCount);
            Assert.AreSame(first, _pool.Get(Vector3.zero, Quaternion.identity), "The pool reuses the released item.");
            Assert.AreEqual(PREWARM_COUNT, _container.childCount, "No new instance was created.");
        }

        /// <summary>
        /// When the pool is exhausted it grows by one instance and warns, but only the first time.
        /// </summary>
        [Test]
        public async Task Get_WhenThePoolIsExhausted_GrowsAndWarnsOnlyOnce()
        {
            _pool.Dispose();
            _pool = CreatePool(1);
            await _pool.PrewarmAsync();
            LogAssert.Expect(LogType.Warning, new Regex("BoxCollider2D pool is exhausted"));

            _pool.Get(Vector3.zero, Quaternion.identity);
            _pool.Get(Vector3.zero, Quaternion.identity);
            _pool.Get(Vector3.zero, Quaternion.identity);

            Assert.AreEqual(3, _container.childCount);
        }

        /// <summary>
        /// Releasing an item that is not in use logs a warning and changes nothing.
        /// </summary>
        [Test]
        public async Task Release_OfAnItemThatIsNotActive_LogsAWarningAndIgnoresIt()
        {
            await _pool.PrewarmAsync();
            var item = _pool.Get(Vector3.zero, Quaternion.identity);
            _pool.Release(item);
            LogAssert.Expect(LogType.Warning, new Regex("not active"));

            _pool.Release(item);

            Assert.AreEqual(PREWARM_COUNT, _pool.PooledCount, "The item must not be pooled twice.");
            Assert.AreEqual(1, _released.Count, "The cleanup must not run twice.");
        }

        /// <summary>
        /// Getting before the pre-warm is a programming error and throws.
        /// </summary>
        [Test]
        public void Get_BeforePrewarm_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _pool.Get(Vector3.zero, Quaternion.identity));
        }

        /// <summary>
        /// A prefab without the pooled component is rejected, and its handle is released.
        /// </summary>
        [Test]
        public void PrewarmAsync_WithAPrefabWithoutTheComponent_ThrowsAndReleasesTheHandle()
        {
            _providePrefabWithoutComponent = true;

            Assert.ThrowsAsync<InvalidOperationException>(() => _pool.PrewarmAsync());

            Assert.AreEqual(0, _assets.OutstandingHandles);
            Assert.AreEqual(0, _container.childCount);
        }

        /// <summary>
        /// Disposing destroys every instance, active or pooled, and releases the prefab.
        /// </summary>
        [Test]
        public async Task Dispose_AfterUse_DestroysTheInstancesAndReleasesThePrefab()
        {
            await _pool.PrewarmAsync();
            _pool.Get(Vector3.zero, Quaternion.identity);

            _pool.Dispose();

            Assert.AreEqual(0, _container.childCount);
            Assert.AreEqual(0, _assets.OutstandingHandles);
        }

        /// <summary>
        /// If the pool is disposed while the prefab is still loading, the prefab is released when the load ends and
        /// nothing is built: no handle and no instance leaks.
        /// </summary>
        [Test]
        public void PrewarmAsync_WhenDisposedDuringTheLoad_ReleasesThePrefabAndBuildsNothing()
        {
            var gate = new TaskCompletionSource<bool>();
            _assets.Gate = gate.Task;
            var prewarm = _pool.PrewarmAsync();

            _pool.Dispose();
            gate.SetResult(true);

            Assert.ThrowsAsync<ObjectDisposedException>(async () => await prewarm);
            Assert.AreEqual(0, _assets.OutstandingHandles, "The prefab must be released.");
            Assert.AreEqual(0, _container.childCount, "No instance may be built after the dispose.");
        }

        /// <summary>
        /// A second pre-warm while the first is still loading is refused, so the prefab is not loaded twice.
        /// </summary>
        [Test]
        public async Task PrewarmAsync_WhileAnotherPrewarmIsLoading_Throws()
        {
            var gate = new TaskCompletionSource<bool>();
            _assets.Gate = gate.Task;
            var first = _pool.PrewarmAsync();

            Assert.ThrowsAsync<InvalidOperationException>(() => _pool.PrewarmAsync());

            gate.SetResult(true);
            await first;
            Assert.AreEqual(PREWARM_COUNT, _container.childCount, "Only the first pre-warm builds the pool.");
            Assert.AreEqual(1, _assets.LoadCount, "The prefab loads once.");
        }

        /// <summary>
        /// Releasing an item that Unity destroyed in the meantime forgets it quietly: it does not throw, does not
        /// run the cleanup and is not put back in the pool.
        /// </summary>
        [Test]
        public async Task Release_OfAnItemDestroyedMeanwhile_ForgetsItWithoutThrowing()
        {
            await _pool.PrewarmAsync();
            var item = _pool.Get(Vector3.zero, Quaternion.identity);
            UnityEngine.Object.DestroyImmediate(item.gameObject);

            Assert.DoesNotThrow(() => _pool.Release(item));

            Assert.AreEqual(0, _pool.Active.Count, "The destroyed item is no longer in use.");
            Assert.AreEqual(PREWARM_COUNT - 1, _pool.PooledCount, "It must not go back to the pool.");
            Assert.AreEqual(0, _released.Count, "The cleanup must not run on a destroyed item.");
        }

        /// <summary>
        /// Releasing everything returns every item in use to the pool, runs the cleanup on each, disables them and
        /// creates and destroys nothing.
        /// </summary>
        [Test]
        public async Task ReleaseAll_WithItemsInUse_ReturnsThemAllToThePoolRunningTheCleanup()
        {
            await _pool.PrewarmAsync();
            var items = new[]
            {
                _pool.Get(Vector3.zero, Quaternion.identity),
                _pool.Get(Vector3.zero, Quaternion.identity),
                _pool.Get(Vector3.zero, Quaternion.identity)
            };

            _pool.ReleaseAll();

            Assert.AreEqual(0, _pool.Active.Count);
            Assert.AreEqual(PREWARM_COUNT, _pool.PooledCount);
            CollectionAssert.AreEquivalent(items, _released, "The cleanup ran once for each item.");
            foreach (var item in items)
                Assert.IsFalse(item.gameObject.activeSelf);

            Assert.AreEqual(PREWARM_COUNT, _container.childCount, "Nothing was created or destroyed.");
            Assert.AreEqual(1, _assets.LoadCount, "The prefab stays loaded.");
            Assert.AreEqual(1, _assets.OutstandingHandles, "The prefab handle is still held until Dispose.");
        }

        /// <summary>
        /// A cleanup that throws is logged but does not stop the restart: every item is still disabled and pooled, so
        /// none is lost and none is left in use.
        /// </summary>
        [Test]
        public async Task ReleaseAll_WhenTheCleanupThrows_LogsItAndStillReturnsEveryItem()
        {
            _pool.Dispose();
            var calls = 0;
            _pool = new PrefabPool<BoxCollider2D>(_assets, new AssetReference("pool-prefab"), _container, PREWARM_COUNT, item =>
            {
                calls++;
                if (calls == 2)
                    throw new InvalidOperationException("cleanup failed");
            });
            await _pool.PrewarmAsync();
            var items = new[]
            {
                _pool.Get(Vector3.zero, Quaternion.identity),
                _pool.Get(Vector3.zero, Quaternion.identity),
                _pool.Get(Vector3.zero, Quaternion.identity)
            };
            LogAssert.Expect(LogType.Exception, new Regex("cleanup failed"));

            Assert.DoesNotThrow(() => _pool.ReleaseAll());

            Assert.AreEqual(3, calls, "The cleanup ran for every item, also after the one that failed.");
            Assert.AreEqual(0, _pool.Active.Count, "No item is left in use.");
            Assert.AreEqual(PREWARM_COUNT, _pool.PooledCount, "No item is lost.");
            foreach (var item in items)
                Assert.IsFalse(item.gameObject.activeSelf);
        }

        /// <summary>
        /// Items that Unity destroyed in the meantime are skipped: the others are still returned and nothing throws.
        /// </summary>
        [Test]
        public async Task ReleaseAll_WithADestroyedItem_SkipsItAndReturnsTheRest()
        {
            await _pool.PrewarmAsync();
            var destroyed = _pool.Get(Vector3.zero, Quaternion.identity);
            var alive = _pool.Get(Vector3.zero, Quaternion.identity);
            UnityEngine.Object.DestroyImmediate(destroyed.gameObject);

            Assert.DoesNotThrow(() => _pool.ReleaseAll());

            Assert.AreEqual(0, _pool.Active.Count);
            Assert.AreEqual(PREWARM_COUNT - 1, _pool.PooledCount, "The destroyed item is not pooled.");
            CollectionAssert.AreEqual(new[] { alive }, _released);
        }

        /// <summary>
        /// Releasing everything with nothing in use, even before the pre-warm, does nothing.
        /// </summary>
        [Test]
        public void ReleaseAll_WithNothingInUse_DoesNothing()
        {
            Assert.DoesNotThrow(() => _pool.ReleaseAll());

            Assert.AreEqual(0, _released.Count);
        }

        /// <summary>
        /// Builds a pool over the fake service whose cleanup records the released items.
        /// </summary>
        /// <param name="prewarmCount">Number of instances to pre-warm.</param>
        private PrefabPool<BoxCollider2D> CreatePool(int prewarmCount)
        {
            return new PrefabPool<BoxCollider2D>(_assets, new AssetReference("pool-prefab"), _container, prewarmCount, item => _released.Add(item));
        }

        /// <summary>
        /// Gives the fake service a prefab-like object: with a BoxCollider2D, or without it when the test asks.
        /// </summary>
        /// <param name="type">Type the pool asked for.</param>
        private UnityEngine.Object ProvideAsset(Type type)
        {
            return _providePrefabWithoutComponent
                ? new GameObject("PrefabWithoutComponent")
                : new GameObject("Prefab", typeof(BoxCollider2D));
        }
    }
}
