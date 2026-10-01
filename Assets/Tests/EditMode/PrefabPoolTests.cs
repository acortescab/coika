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
