using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// A small jar with a real <see cref="PieceFactory"/> and <see cref="MergeSystem"/> for the merge tests, built
    /// from in-memory tiers and a test asset service, so nothing is loaded through Addressables. The test owns the
    /// world and must call <see cref="Dispose"/>.
    /// </summary>
    public sealed class MergeTestWorld : IDisposable
    {
        /// <summary>Number of tiers of the test theme, like the real one (tier 10 is the Black Hole).</summary>
        public const int TIER_COUNT = 11;

        private readonly List<UnityEngine.Object> _created = new();
        private readonly SimulationMode2D _simulationMode;

        /// <summary>
        /// Builds the jar, the tiers, the factory and the merge system. The pieces are pooled up front.
        /// </summary>
        /// <param name="prewarmCount">Pieces the pool builds before the test starts.</param>
        public MergeTestWorld(int prewarmCount)
        {
            _simulationMode = Physics2D.simulationMode;

            Config = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(Config);
            SetField(Config, "_jarSize", new Vector2(10f, 12.5f));
            SetField(Config, "_dropLineOffset", 1.5f);

            var jarObject = new GameObject("Jar");
            _created.Add(jarObject);
            Jar = jarObject.AddComponent<Jar>();
            JarBuilder.Build(Jar, Config);

            var tiers = new List<TierDefinition>();
            for (var i = 0; i < TIER_COUNT; i++)
            {
                var tier = ScriptableObject.CreateInstance<TierDefinition>();
                _created.Add(tier);
                SetField(tier, "_index", i);
                SetField(tier, "_diameterUnits", 0.6f + 0.1f * i);
                tiers.Add(tier);
            }

            Tiers = tiers;

            var container = new GameObject("PieceContainer");
            _created.Add(container);
            Factory = new PieceFactory(new TestAssetService(_created), new AssetReference(), Config, container.transform, prewarmCount);

            var mergeObject = new GameObject("MergeSystem");
            _created.Add(mergeObject);
            Merge = mergeObject.AddComponent<MergeSystem>();
        }

        /// <summary>The config with the jar size and the default overflow grace.</summary>
        public GameConfig Config { get; }

        /// <summary>A point well inside the jar, away from the floor and the walls, where the tests place their pieces.</summary>
        public Vector2 Origin => new(0f, Jar.FloorY + 5f);

        /// <summary>The jar the pieces fall in.</summary>
        public Jar Jar { get; }

        /// <summary>The tiers, in tier order.</summary>
        public IReadOnlyList<TierDefinition> Tiers { get; }

        /// <summary>The factory that creates the pieces.</summary>
        public PieceFactory Factory { get; }

        /// <summary>The system under test.</summary>
        public MergeSystem Merge { get; }

        /// <summary>
        /// Pre-warms the factory and starts the merge system. Call it once, before creating pieces.
        /// </summary>
        public async Task StartAsync()
        {
            await Factory.PrewarmAsync(Tiers);
            Merge.Initialize(Factory, Tiers, Config);
        }

        /// <summary>
        /// Creates a piece with no gravity, so a test can place pieces anywhere and they stay put until they touch.
        /// </summary>
        /// <param name="tier">Tier index.</param>
        /// <param name="position">Position in world units.</param>
        /// <param name="velocity">Starting velocity.</param>
        public Piece CreateFloating(int tier, Vector2 position, Vector2 velocity)
        {
            var piece = Factory.Create(Tiers[tier], position, velocity);
            piece.Rigidbody.gravityScale = 0f;
            return piece;
        }

        /// <summary>
        /// Counts the active pieces of a tier.
        /// </summary>
        /// <param name="tier">Tier index.</param>
        public int CountActive(int tier)
        {
            var count = 0;
            for (var i = 0; i < Factory.ActivePieces.Count; i++)
            {
                if (Factory.ActivePieces[i].Tier.Index == tier)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Takes the physics under script control, so a test advances it step by step and runs the merge system
        /// itself, without waiting for real time.
        /// </summary>
        public void UseScriptedPhysics()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
        }

        /// <summary>
        /// One physics step followed by the merge pass, in the same order as the game: contacts are reported during
        /// the step and merged by the next FixedUpdate.
        /// </summary>
        public void Step()
        {
            Merge.ProcessQueue();
            Physics2D.Simulate(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Restores the physics mode and destroys everything the world created.
        /// </summary>
        public void Dispose()
        {
            Physics2D.simulationMode = _simulationMode;
            Factory.Dispose();
            foreach (var created in _created)
            {
                UnityEngine.Object.Destroy(created);
            }

            _created.Clear();
        }

        /// <summary>
        /// Sets a private instance field by reflection.
        /// </summary>
        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        /// <summary>
        /// Asset service that hands out an inactive Piece prefab and a sprite, and counts nothing.
        /// </summary>
        private sealed class TestAssetService : IAssetService
        {
            private readonly List<UnityEngine.Object> _created;

            public TestAssetService(List<UnityEngine.Object> created)
            {
                _created = created;
            }

            public Task<T> LoadAsset<T>(AssetReference assetReference)
            {
                return Task.FromResult(Provide<T>());
            }

            public Task<T> LoadAsset<T>(string label)
            {
                return Task.FromResult(Provide<T>());
            }

            public void ReleaseAsset(UnityEngine.Object objectToRelease)
            {
            }

            public Task PreloadAsset(AssetReference assetReference, Action<float> onProgress = null)
            {
                return Task.CompletedTask;
            }

            public Task PreloadAsset(string label, Action<float> onProgress = null)
            {
                return Task.CompletedTask;
            }

            private T Provide<T>()
            {
                UnityEngine.Object asset;
                if (typeof(T) == typeof(GameObject))
                {
                    // Inactive, so the original never takes part in the physics: only its clones do.
                    var prefab = new GameObject("PiecePrefab", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
                    prefab.SetActive(false);
                    asset = prefab;
                }
                else if (typeof(T) == typeof(Sprite))
                {
                    var texture = new Texture2D(16, 16);
                    _created.Add(texture);
                    asset = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), TierDefinition.PixelsPerUnit);
                }
                else
                {
                    throw new NotSupportedException(typeof(T).Name);
                }

                _created.Add(asset);
                return (T)(object)asset;
            }
        }
    }
}
