using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using UnityEngine;

namespace Coika.UI
{
    /// <summary>
    /// Loads the sprite of every tier once, through the asset service (C-01), so the HUD and the Game Over view can
    /// ask for a tier sprite synchronously and without allocating. Disposing it releases every sprite it loaded,
    /// even when the disposal happens while the load is still running.
    /// </summary>
    public sealed class TierSpriteCache : IDisposable
    {
        private readonly IAssetService _assets;
        private readonly List<Sprite> _sprites = new List<Sprite>();
        private bool _disposed;

        /// <summary>
        /// Creates an empty cache.
        /// </summary>
        /// <param name="assets">Asset service that loads and releases the sprites.</param>
        /// <exception cref="ArgumentNullException">The service is null.</exception>
        public TierSpriteCache(IAssetService assets)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        /// <summary>
        /// Loads the sprite of each tier, in tier order. A sprite that finishes loading after the cache was
        /// disposed is released at once.
        /// </summary>
        /// <param name="tiers">Every tier, in tier order.</param>
        /// <exception cref="ArgumentNullException">The tiers are null.</exception>
        /// <exception cref="AssetLoadException">A sprite failed to load; the ones already loaded stay cached.</exception>
        public async Task LoadAsync(IReadOnlyList<TierDefinition> tiers)
        {
            if (tiers == null)
            {
                throw new ArgumentNullException(nameof(tiers));
            }

            for (var i = 0; i < tiers.Count; i++)
            {
                var sprite = await _assets.LoadAsset<Sprite>(tiers[i].Sprite);
                if (_disposed)
                {
                    _assets.ReleaseAsset(sprite);
                    return;
                }

                _sprites.Add(sprite);
            }
        }

        /// <summary>
        /// The sprite of a tier, or null when it is not loaded (yet) or the tier does not exist.
        /// </summary>
        /// <param name="tier">Tier index.</param>
        public Sprite Get(int tier)
        {
            return tier >= 0 && tier < _sprites.Count ? _sprites[tier] : null;
        }

        /// <summary>
        /// Releases every loaded sprite. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            for (var i = 0; i < _sprites.Count; i++)
            {
                _assets.ReleaseAsset(_sprites[i]);
            }

            _sprites.Clear();
        }
    }
}
