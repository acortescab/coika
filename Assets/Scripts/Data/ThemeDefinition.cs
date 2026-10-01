using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Data
{
    /// <summary>
    /// A theme (skin): the ordered set of 11 tiers plus background, jar art and music.
    /// Everything is referenced through Addressables (C-01) and loaded through <see cref="IAssetService"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeDefinition", menuName = "Scriptable Objects/Theme Definition")]
    public class ThemeDefinition : ScriptableObject
    {
        /// <summary>Number of tiers every theme must define (GDD §3.2).</summary>
        public const int TIER_COUNT = 11;

        [SerializeField]
        private List<AssetReferenceT<TierDefinition>> _tiers = new();
        [SerializeField]
        private AssetReferenceSprite _background; // Empty until M3
        [SerializeField]
        private AssetReferenceSprite _jarSprite; // Empty until M3
        [SerializeField]
        private AssetReferenceT<AudioClip> _music; // Empty until M3

        /// <summary>
        /// Loads the tiers in order. If one load fails, the tiers already loaded are released before rethrowing,
        /// so a failure never leaves handles behind.
        /// </summary>
        /// <param name="assets">Service used to load the tiers.</param>
        /// <returns>The tiers, in tier order. Release them with <see cref="ReleaseTiers"/>.</returns>
        /// <exception cref="AssetLoadException">A tier failed to load.</exception>
        public async Task<IReadOnlyList<TierDefinition>> LoadTiersAsync(IAssetService assets)
        {
            var loaded = new List<TierDefinition>(_tiers.Count);

            try
            {
                foreach (var tierReference in _tiers)
                    loaded.Add(await assets.LoadAsset<TierDefinition>(tierReference));
            }
            catch
            {
                ReleaseTiers(assets, loaded);
                throw;
            }

            return loaded;
        }

        /// <summary>
        /// Releases tiers previously returned by <see cref="LoadTiersAsync"/>.
        /// </summary>
        /// <param name="assets">Service that loaded the tiers.</param>
        /// <param name="tiers">The tiers to release.</param>
        public void ReleaseTiers(IAssetService assets, IReadOnlyList<TierDefinition> tiers)
        {
            foreach (var tier in tiers)
                assets.ReleaseAsset(tier);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Checks the theme and describes every problem found: tier count, tier order, missing sprites and sprite
        /// size. Editor only, because it reads the assets directly through the AssetDatabase.
        /// </summary>
        /// <returns>One message per problem; empty when the theme is valid.</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (_tiers.Count != TIER_COUNT)
                errors.Add($"Theme must have exactly {TIER_COUNT} tiers, found {_tiers.Count}.");

            for (int i = 0; i < _tiers.Count; i++)
            {
                var tier = LoadEditorAsset<TierDefinition>(_tiers[i]);
                if (tier == null)
                {
                    errors.Add($"Tier slot {i} is empty or its asset is missing.");
                    continue;
                }

                if (tier.Index != i)
                    errors.Add($"Tier slot {i} holds '{tier.name}' with index {tier.Index}; indices must be 0-{TIER_COUNT - 1} in order.");

                var sprite = LoadEditorAsset<Sprite>(tier.Sprite);
                if (sprite == null)
                {
                    errors.Add($"Tier '{tier.name}' has no sprite.");
                    continue;
                }

                var expectedWidth = Mathf.RoundToInt(tier.DiameterUnits * TierDefinition.PixelsPerUnit);
                var actualWidth = Mathf.RoundToInt(sprite.rect.width);
                if (actualWidth != expectedWidth)
                    errors.Add($"Tier '{tier.name}' sprite is {actualWidth}px wide, expected {expectedWidth}px (diameter x {TierDefinition.PixelsPerUnit}).");
            }

            return errors;
        }

        /// <summary>
        /// Logs every validation problem as an error whenever the asset is edited.
        /// </summary>
        private void OnValidate()
        {
            foreach (var error in Validate())
                Debug.LogError($"{name}: {error}", this);
        }

        /// <summary>
        /// Loads the asset an Addressable reference points to, straight from the AssetDatabase.
        /// </summary>
        /// <typeparam name="T">Type of the asset to load.</typeparam>
        /// <param name="reference">The reference to resolve. May be null or empty.</param>
        /// <returns>The asset, or null when the reference is empty or its asset does not exist.</returns>
        private static T LoadEditorAsset<T>(AssetReference reference) where T : Object
        {
            if (reference == null || string.IsNullOrEmpty(reference.AssetGUID))
                return null;

            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        }
#endif
    }
}
