using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the <see cref="PieceFactory"/> pool (issue #4, scope 3) with a fake asset service, so no bundles are
    /// needed: pre-warming, creating and releasing, reuse without new instances, growth, and that every loaded
    /// handle is released.
    /// </summary>
    public class PieceFactoryTests
    {
        private const int PREWARM_COUNT = 5;

        private readonly List<UnityEngine.Object> _created = new();
        private FakeAssetService _assets;
        private GameConfig _config;
        private Transform _container;
        private List<TierDefinition> _tiers;
        private PieceFactory _factory;

        /// <summary>
        /// Creates the fake service, a config, the container and three tiers of diameter 1, 2 and 3.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new FakeAssetService { Provider = ProvideAsset };
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, null);
            _container = new GameObject("PieceContainer").transform;
            _tiers = new List<TierDefinition> { CreateTier(1f), CreateTier(2f), CreateTier(3f) };
            _factory = CreateFactory(PREWARM_COUNT);
        }

        /// <summary>
        /// Disposes the factory and destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
            _assets.Cleanup();

            foreach (var created in _created)
                UnityEngine.Object.DestroyImmediate(created);

            _created.Clear();
            UnityEngine.Object.DestroyImmediate(_container.gameObject);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Pre-warming builds the requested number of disabled pieces under the container, and loads the prefab
        /// and one sprite per tier.
        /// </summary>
        [Test]
        public async Task PrewarmAsync_Always_BuildsDisabledPiecesUnderTheContainerAndLoadsPrefabAndSprites()
        {
            await _factory.PrewarmAsync(_tiers);

            Assert.AreEqual(PREWARM_COUNT, _container.childCount);
            Assert.AreEqual(PREWARM_COUNT, _factory.PooledCount);
            Assert.AreEqual(0, _factory.ActivePieces.Count);
            for (int i = 0; i < _container.childCount; i++)
                Assert.IsFalse(_container.GetChild(i).gameObject.activeSelf, "Pooled pieces are disabled.");

            Assert.AreEqual(1 + _tiers.Count, _assets.LoadCount, "The prefab and a sprite per tier.");
        }

        /// <summary>
        /// A created piece is active, configured for the tier, placed and moving as asked, and listed as active.
        /// </summary>
        [Test]
        public async Task Create_AfterPrewarm_ReturnsAnActiveConfiguredPieceAtThePosition()
        {
            await _factory.PrewarmAsync(_tiers);

            var piece = _factory.Create(_tiers[1], new Vector2(1f, 2f), new Vector2(0.5f, -1f));

            Assert.IsTrue(piece.gameObject.activeSelf);
            Assert.AreSame(_tiers[1], piece.Tier);
            Assert.AreEqual(1f, piece.Collider.radius, 0.0001f, "Diameter 2 gives radius 1.");
            Assert.AreEqual(new Vector3(1f, 2f, 0f), piece.transform.position);
            Assert.AreEqual(new Vector2(0.5f, -1f), piece.Rigidbody.linearVelocity);
            CollectionAssert.Contains(_factory.ActivePieces, piece);
            Assert.AreEqual(PREWARM_COUNT - 1, _factory.PooledCount);
        }

        /// <summary>
        /// Releasing a piece makes it inactive, disables it, removes its Collided subscribers and returns it to the pool.
        /// </summary>
        [Test]
        public async Task Release_OfAnActivePiece_DisablesItClearsSubscribersAndReturnsItToThePool()
        {
            await _factory.PrewarmAsync(_tiers);
            var piece = _factory.Create(_tiers[0], Vector2.zero, Vector2.zero);
            piece.Collided += (self, other) => { };
            Assert.IsNotNull(GetCollidedField(piece), "The subscriber was added.");

            _factory.Release(piece);

            Assert.IsFalse(piece.gameObject.activeSelf);
            Assert.IsNull(GetCollidedField(piece), "The subscribers were removed.");
            CollectionAssert.DoesNotContain(_factory.ActivePieces, piece);
            Assert.AreEqual(PREWARM_COUNT, _factory.PooledCount);
        }

        /// <summary>
        /// A reused piece is the same instance with a fresh state: not merged, not held, no leftover velocity,
        /// dynamic body, piece layer and the new tier.
        /// </summary>
        [Test]
        public async Task Create_AfterRelease_ReusesThePieceWithAFreshState()
        {
            await _factory.PrewarmAsync(_tiers);
            var first = _factory.Create(_tiers[0], Vector2.zero, new Vector2(5f, 5f));
            first.MarkMerged();
            first.SetHeld(true);
            _factory.Release(first);

            // Pop order is LIFO: the piece just released is the next one created.
            var second = _factory.Create(_tiers[2], new Vector2(1f, 1f), Vector2.zero);

            Assert.AreSame(first, second, "The pool reuses the released piece.");
            Assert.AreSame(_tiers[2], second.Tier);
            Assert.IsFalse(second.Merged, "Merged");
            Assert.IsFalse(second.IsHeld, "Held");
            Assert.AreEqual(Vector2.zero, second.Rigidbody.linearVelocity, "Velocity");
            Assert.AreEqual(RigidbodyType2D.Dynamic, second.Rigidbody.bodyType, "Body type");
            Assert.AreEqual(LayerMask.NameToLayer(Piece.LAYER_NAME), second.gameObject.layer, "Layer");
            Assert.IsTrue(second.Collider.enabled, "Collider");
        }

        /// <summary>
        /// Spawning and releasing 1,000 times after the pre-warm never instantiates another piece or loads another asset.
        /// </summary>
        [Test]
        public async Task CreateAndRelease_OneThousandTimes_NeverInstantiatesNorLoadsMore()
        {
            await _factory.PrewarmAsync(_tiers);
            var loadsAfterPrewarm = _assets.LoadCount;

            for (int i = 0; i < 1000; i++)
                _factory.Release(_factory.Create(_tiers[i % _tiers.Count], Vector2.zero, Vector2.zero));

            Assert.AreEqual(PREWARM_COUNT, _container.childCount, "No new instances after the pre-warm.");
            Assert.AreEqual(loadsAfterPrewarm, _assets.LoadCount, "No new loads after the pre-warm.");
            Assert.AreEqual(0, _factory.ActivePieces.Count);
        }

        /// <summary>
        /// In steady state, creating and releasing pieces allocates no managed memory. This approximates the
        /// Profiler criterion of 0 GC allocations per frame; confirm it in the Profiler as well.
        /// </summary>
        [Test]
        public async Task CreateAndRelease_InSteadyState_AllocatesNoManagedMemory()
        {
            await _factory.PrewarmAsync(_tiers);
            for (int i = 0; i < 20; i++)
                _factory.Release(_factory.Create(_tiers[0], Vector2.zero, Vector2.zero));

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                _factory.Release(_factory.Create(_tiers[i % _tiers.Count], Vector2.zero, Vector2.zero));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated, "Managed bytes allocated by 1,000 create/release cycles.");
        }

        /// <summary>
        /// When the pool is exhausted the factory grows by one piece and warns, but only the first time.
        /// </summary>
        [Test]
        public async Task Create_WhenThePoolIsExhausted_GrowsAndWarnsOnlyOnce()
        {
            _factory.Dispose();
            _factory = CreateFactory(1);
            await _factory.PrewarmAsync(_tiers);
            LogAssert.Expect(LogType.Warning, new Regex("pool is exhausted"));

            _factory.Create(_tiers[0], Vector2.zero, Vector2.zero);
            _factory.Create(_tiers[0], Vector2.zero, Vector2.zero);
            _factory.Create(_tiers[0], Vector2.zero, Vector2.zero);

            Assert.AreEqual(3, _container.childCount, "The pool grew to fit the three pieces.");
        }

        /// <summary>
        /// Creating before the pre-warm, or for a tier that was not part of it, is a programming error and throws.
        /// </summary>
        [Test]
        public async Task Create_BeforePrewarmOrForAnUnknownTier_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _factory.Create(_tiers[0], Vector2.zero, Vector2.zero), "Before the pre-warm");

            await _factory.PrewarmAsync(new List<TierDefinition> { _tiers[0] });

            Assert.Throws<InvalidOperationException>(() => _factory.Create(_tiers[1], Vector2.zero, Vector2.zero), "Unknown tier");
        }

        /// <summary>
        /// Releasing a piece that is not active logs a warning and changes nothing.
        /// </summary>
        [Test]
        public async Task Release_OfAPieceThatIsNotActive_LogsAWarningAndIgnoresIt()
        {
            await _factory.PrewarmAsync(_tiers);
            var piece = _factory.Create(_tiers[0], Vector2.zero, Vector2.zero);
            _factory.Release(piece);
            LogAssert.Expect(LogType.Warning, new Regex("not active"));

            _factory.Release(piece);

            Assert.AreEqual(PREWARM_COUNT, _factory.PooledCount, "The piece must not be pooled twice.");
        }

        /// <summary>
        /// If a load fails during the pre-warm, what was already loaded is released and the error is rethrown.
        /// </summary>
        [Test]
        public void PrewarmAsync_WhenALoadFails_ReleasesWhatWasLoadedAndRethrows()
        {
            _assets.FailOnAttempt = 3; // The sprites load first: the first two load, the third fails.

            Assert.ThrowsAsync<AssetLoadException>(() => _factory.PrewarmAsync(_tiers));

            Assert.AreEqual(0, _assets.OutstandingHandles, "No handle may be left behind.");
            Assert.AreEqual(0, _container.childCount, "No piece was built.");
        }

        /// <summary>
        /// Disposing destroys every piece and releases the prefab and the sprites.
        /// </summary>
        [Test]
        public async Task Dispose_AfterUse_DestroysThePiecesAndReleasesEveryHandle()
        {
            await _factory.PrewarmAsync(_tiers);
            _factory.Create(_tiers[0], Vector2.zero, Vector2.zero);
            _factory.Create(_tiers[1], Vector2.zero, Vector2.zero);

            _factory.Dispose();

            Assert.AreEqual(0, _container.childCount, "Every piece, active or pooled, is destroyed.");
            Assert.AreEqual(0, _assets.OutstandingHandles, "The prefab and the sprites are released.");
        }

        /// <summary>
        /// Ten consecutive runs, each pre-warming, playing and disposing, leave the loads and releases balanced.
        /// </summary>
        [Test]
        public async Task TenConsecutiveRuns_LeaveNoOutstandingHandles()
        {
            _factory.Dispose();

            for (int run = 0; run < 10; run++)
            {
                _factory = CreateFactory(PREWARM_COUNT);
                await _factory.PrewarmAsync(_tiers);
                for (int i = 0; i < 20; i++)
                    _factory.Release(_factory.Create(_tiers[i % _tiers.Count], Vector2.zero, Vector2.zero));

                _factory.Dispose();

                Assert.AreEqual(0, _assets.OutstandingHandles, $"Run {run}");
            }

            Assert.AreEqual(_assets.LoadCount, _assets.ReleaseCount);
        }

        /// <summary>
        /// A negative pre-warm count is a programming error and throws.
        /// </summary>
        [Test]
        public void Constructor_WithNegativePrewarmCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateFactory(-1));
        }

        /// <summary>
        /// Builds a factory over the fake service.
        /// </summary>
        /// <param name="prewarmCount">Number of pieces to pre-warm.</param>
        private PieceFactory CreateFactory(int prewarmCount)
        {
            return new PieceFactory(_assets, new AssetReference("piece-prefab"), _config, _container, prewarmCount);
        }

        /// <summary>
        /// Gives the fake service a piece-like object for the prefab and a sprite for the sprites.
        /// </summary>
        /// <param name="type">Type the factory asked for.</param>
        private UnityEngine.Object ProvideAsset(Type type)
        {
            if (type == typeof(GameObject))
                return new GameObject("PiecePrefab", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));

            if (type == typeof(Sprite))
            {
                var texture = new Texture2D(16, 16);
                _created.Add(texture);
                return Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), TierDefinition.PixelsPerUnit);
            }

            throw new NotSupportedException(type.Name);
        }

        /// <summary>
        /// Creates an in-memory tier with the given diameter and a sprite reference, by writing its serialized fields.
        /// </summary>
        /// <param name="diameterUnits">Diameter of the tier in world units.</param>
        private TierDefinition CreateTier(float diameterUnits)
        {
            var tier = ScriptableObject.CreateInstance<TierDefinition>();
            _created.Add(tier);

            var serializedTier = new SerializedObject(tier);
            serializedTier.FindProperty("_diameterUnits").floatValue = diameterUnits;
            serializedTier.FindProperty("_sprite.m_AssetGUID").stringValue = "tier-sprite";
            serializedTier.ApplyModifiedPropertiesWithoutUndo();
            return tier;
        }

        /// <summary>
        /// Reads the backing field of <see cref="Piece.Collided"/>, which is null when nobody is subscribed.
        /// </summary>
        /// <param name="piece">The piece to inspect.</param>
        private static object GetCollidedField(Piece piece)
        {
            return typeof(Piece)
                .GetField("Collided", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(piece);
        }
    }
}
