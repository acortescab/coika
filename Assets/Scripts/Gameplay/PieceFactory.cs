using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Gameplay
{
    /// <summary>
    /// Creates and recycles pieces (budget: 0 GC allocations per frame, at most 60 active bodies). The pooling
    /// itself is done by a <see cref="PrefabPool{T}"/>; this class adds what is specific to pieces: the sprite of
    /// every tier and the initialization of each piece.
    /// <para>
    /// C-01: the piece prefab and the tier sprites are Addressable, so nothing is loaded lazily during play.
    /// <see cref="PrewarmAsync"/> loads them through <see cref="IAssetService"/> during the load phase and must be
    /// awaited before the run starts; <see cref="Create"/> only recycles. The loaded handles are released by
    /// <see cref="Dispose"/>, which the owner calls when the factory is no longer needed.
    /// </para>
    /// <para>
    /// Pieces are created and released from Update or FixedUpdate, never from inside a collision callback.
    /// </para>
    /// </summary>
    public sealed class PieceFactory : IDisposable
    {
        private readonly IAssetService _assets;
        private readonly GameConfig _config;
        private readonly PrefabPool<Piece> _pool;
        private readonly Dictionary<TierDefinition, Sprite> _sprites = new();

        private bool _prewarmed;
        private bool _disposed;

        /// <summary>
        /// Creates a factory. It holds nothing until <see cref="PrewarmAsync"/> runs.
        /// </summary>
        /// <param name="assets">Service used to load and release the prefab and the sprites.</param>
        /// <param name="prefabReference">Addressable reference to the Piece prefab.</param>
        /// <param name="config">Config passed to every piece when it is initialized.</param>
        /// <param name="container">Object that parents every piece, active or pooled.</param>
        /// <param name="prewarmCount">Number of pieces built when pre-warming.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The pre-warm count is negative.</exception>
        public PieceFactory(IAssetService assets, AssetReference prefabReference, GameConfig config, Transform container, int prewarmCount = PrefabPool<Piece>.DEFAULT_PREWARM_COUNT)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _pool = new PrefabPool<Piece>(assets, prefabReference, container, prewarmCount, ClearSubscribers);
        }

        /// <summary>The pieces that are in play now, for the overflow and merge systems. Read-only.</summary>
        public IReadOnlyList<Piece> ActivePieces => _pool.Active;

        /// <summary>Number of pieces waiting in the pool.</summary>
        public int PooledCount => _pool.PooledCount;

        /// <summary>
        /// Loads the sprite of every given tier and the Piece prefab, and builds the pool of disabled pieces. If a
        /// load fails, everything loaded so far is released before the error is rethrown. Call it once, during the
        /// load phase and before the run starts.
        /// </summary>
        /// <param name="tiers">Every tier the factory will be asked to create.</param>
        /// <exception cref="ArgumentNullException">The tiers are null.</exception>
        /// <exception cref="InvalidOperationException">The factory was already pre-warmed, or the prefab has no Piece component.</exception>
        /// <exception cref="AssetLoadException">The prefab or a sprite failed to load.</exception>
        public async Task PrewarmAsync(IReadOnlyList<TierDefinition> tiers)
        {
            ThrowIfDisposed();

            if (tiers == null)
                throw new ArgumentNullException(nameof(tiers));

            if (_prewarmed)
                throw new InvalidOperationException("The factory is already pre-warmed.");

            try
            {
                foreach (var tier in tiers)
                {
                    if (!_sprites.ContainsKey(tier))
                        _sprites.Add(tier, await _assets.LoadAsset<Sprite>(tier.Sprite));
                }

                await _pool.PrewarmAsync();
            }
            catch
            {
                ReleaseSprites();
                throw;
            }

            _prewarmed = true;
        }

        /// <summary>
        /// Takes a piece from the pool, places it and configures it for the tier. If the pool is empty it grows by
        /// one piece and logs a warning the first time, because that instantiates during play.
        /// </summary>
        /// <param name="tier">Tier of the piece. It must have been given to <see cref="PrewarmAsync"/>.</param>
        /// <param name="position">Position in world units.</param>
        /// <param name="velocity">Starting velocity.</param>
        /// <returns>The active piece.</returns>
        /// <exception cref="ArgumentNullException">The tier is null.</exception>
        /// <exception cref="InvalidOperationException">The factory was not pre-warmed, or does not know the tier.</exception>
        public Piece Create(TierDefinition tier, Vector2 position, Vector2 velocity)
        {
            ThrowIfDisposed();

            if (tier == null)
                throw new ArgumentNullException(nameof(tier));

            if (!_prewarmed)
                throw new InvalidOperationException("Await PrewarmAsync before creating pieces.");

            if (!_sprites.TryGetValue(tier, out var sprite))
                throw new InvalidOperationException($"Tier '{tier.name}' was not part of PrewarmAsync.");

            // The first placement may use the transform: the body takes its pose from it when it is enabled.
            var piece = _pool.Get(new Vector3(position.x, position.y, 0f), Quaternion.identity);
            piece.Initialize(tier, sprite, _config);
            piece.Rigidbody.linearVelocity = velocity;
            return piece;
        }

        /// <summary>
        /// Takes a piece back: it stops being active, loses every <see cref="Piece.Collided"/> subscriber, is
        /// disabled and goes back to the pool. Its state is reset when it is created again. A piece that is not
        /// active in this factory is ignored with a warning.
        /// </summary>
        /// <param name="piece">The piece to take back.</param>
        /// <exception cref="ArgumentNullException">The piece is null.</exception>
        public void Release(Piece piece)
        {
            _pool.Release(piece);
        }

        /// <summary>
        /// Destroys every piece, active or pooled, and releases the prefab and the sprites. The factory cannot be
        /// used afterwards. Calling it again does nothing.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pool.Dispose();
            ReleaseSprites();
        }

        /// <summary>
        /// Removes the subscribers of a piece that goes back to the pool, so a reused piece never keeps listeners
        /// of its previous life.
        /// </summary>
        /// <param name="piece">The piece that was released.</param>
        private static void ClearSubscribers(Piece piece)
        {
            piece.ClearCollidedSubscribers();
        }

        /// <summary>
        /// Releases the loaded sprites back to the asset service, so no handle is left behind.
        /// </summary>
        private void ReleaseSprites()
        {
            foreach (var sprite in _sprites.Values)
                _assets.ReleaseAsset(sprite);

            _sprites.Clear();
        }

        /// <summary>
        /// Throws when the factory was disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The factory was disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PieceFactory));
        }
    }
}
