using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Runs the whole pipeline with the real asset service: the Piece prefab and the tiers are loaded through
    /// Addressables, the factory builds its pool, and the smallest and largest pieces are dropped from the Drop
    /// Line into the jar (issue #4, scope 3). Run with the Addressables Play Mode Script set to "Use Asset Database".
    /// </summary>
    public class PieceFactoryIntegrationTests
    {
        private const string PiecePrefabPath = "Assets/Prefabs/Piece/Piece.prefab";
        private const string SmallestTierKey = "Assets/Data/Tiers/Tier_00_Dust.asset";
        private const string LargestTierKey = "Assets/Data/Tiers/Tier_10_BlackHole.asset";
        private const float SETTLE_SECONDS = 3f;
        private const float POSITION_TOLERANCE = 0.1f;

        private readonly List<Object> _created = new();
        private readonly List<TierDefinition> _loadedTiers = new();
        private AssetService _assets;
        private PieceFactory _factory;

        /// <summary>
        /// Disposes the factory, releases the tiers and destroys everything the test created.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _factory?.Dispose();

            foreach (var tier in _loadedTiers)
                _assets.ReleaseAsset(tier);

            _loadedTiers.Clear();

            foreach (var created in _created)
                Object.Destroy(created);

            _created.Clear();
        }

        /// <summary>
        /// The smallest and the largest tier, created by the factory at the Drop Line, stay inside the jar at
        /// every physics step and end up on the floor: they never tunnel through the floor or the walls.
        /// </summary>
        [UnityTest]
        public IEnumerator Create_WithSmallestAndLargestTier_FromDropLine_NeverLeavesTheJar()
        {
            _assets = new AssetService();
            var loading = LoadTiersAsync();
            yield return new WaitUntil(() => loading.IsCompleted);
            Assert.IsFalse(loading.IsFaulted, loading.Exception?.ToString());

            var config = CreateConfig();
            var jarObject = new GameObject("Jar");
            _created.Add(jarObject);
            var jar = jarObject.AddComponent<Jar>();
            JarBuilder.Build(jar, config);

            var container = new GameObject("PieceContainer");
            _created.Add(container);
            _factory = new PieceFactory(_assets, new AssetReference(GetPrefabGuid()), config, container.transform, 4);
            var prewarm = _factory.PrewarmAsync(_loadedTiers);
            yield return new WaitUntil(() => prewarm.IsCompleted);
            Assert.IsFalse(prewarm.IsFaulted, prewarm.Exception?.ToString());

            foreach (var tier in _loadedTiers)
            {
                var piece = _factory.Create(tier, new Vector2(0f, jar.DropLineY), Vector2.zero);
                var radius = tier.DiameterUnits * 0.5f;

                var elapsed = 0f;
                while (elapsed < SETTLE_SECONDS)
                {
                    yield return new WaitForFixedUpdate();
                    elapsed += Time.fixedDeltaTime;

                    AssertInsideJar(jar, piece.transform.position, radius, tier.name);
                }

                Assert.AreEqual(jar.FloorY + radius, piece.transform.position.y, POSITION_TOLERANCE, tier.name + " should be on the floor.");
                _factory.Release(piece);
            }
        }

        /// <summary>
        /// Loads the smallest and the largest tier through the asset service.
        /// </summary>
        private async Task LoadTiersAsync()
        {
            _loadedTiers.Add(await _assets.LoadAsset<TierDefinition>(SmallestTierKey));
            _loadedTiers.Add(await _assets.LoadAsset<TierDefinition>(LargestTierKey));
        }

        /// <summary>
        /// Checks that a piece of the given radius centred at the position is inside the jar interior and above
        /// the floor, within a small tolerance.
        /// </summary>
        /// <param name="jar">The jar that holds the piece.</param>
        /// <param name="position">Centre of the piece in world units.</param>
        /// <param name="radius">Radius of the piece.</param>
        /// <param name="name">Name of the tier, for the failure message.</param>
        private static void AssertInsideJar(Jar jar, Vector3 position, float radius, string name)
        {
            Assert.GreaterOrEqual(position.x - radius, jar.InteriorMin.x - POSITION_TOLERANCE, name + " left through the left wall.");
            Assert.LessOrEqual(position.x + radius, jar.InteriorMax.x + POSITION_TOLERANCE, name + " left through the right wall.");
            Assert.GreaterOrEqual(position.y - radius, jar.FloorY - POSITION_TOLERANCE, name + " fell through the floor.");
        }

        /// <summary>
        /// Returns the GUID of the Piece prefab. Only the Editor can look it up, and these tests only run there.
        /// </summary>
        private static string GetPrefabGuid()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.AssetPathToGUID(PiecePrefabPath);
#else
            return string.Empty;
#endif
        }

        /// <summary>
        /// Creates an in-memory GameConfig with a jar of the GDD size by setting its private serialized fields.
        /// </summary>
        private GameConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(config);
            SetField(config, "_jarSize", new Vector2(10f, 12.5f));
            SetField(config, "_dropLineOffset", 1.5f);
            return config;
        }

        /// <summary>
        /// Sets a private instance field by reflection.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the private field.</param>
        /// <param name="value">Value to assign.</param>
        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }
    }
}
