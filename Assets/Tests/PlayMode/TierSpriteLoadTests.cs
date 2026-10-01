using System.Collections;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Loads the theme, a tier and its sprite through Addressables at runtime, the way the game will.
    /// Run with the Addressables Play Mode Script set to "Use Asset Database".
    /// </summary>
    public class TierSpriteLoadTests
    {
        private const string ThemeKey = "Assets/Data/Themes/Theme_Cosmic.asset";
        private const int TierIndex = 3; // Moon

        /// <summary>
        /// The theme loads, its 11 tiers load, the sprite of one tier loads with the expected pixel size, and
        /// everything loaded can be released.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadTierSprite_ThroughAssetService_LoadsSpriteWithExpectedSize()
        {
            var run = LoadAndReleaseAsync();
            yield return new WaitUntil(() => run.IsCompleted);

            Assert.IsFalse(run.IsFaulted, run.Exception?.ToString());
        }

        /// <summary>
        /// Loads the theme, its tiers and one tier sprite, checks them, and releases everything even when a
        /// check fails.
        /// </summary>
        private static async Task LoadAndReleaseAsync()
        {
            var assets = new AssetService();
            ThemeDefinition theme = null;
            System.Collections.Generic.IReadOnlyList<TierDefinition> tiers = null;
            Sprite sprite = null;

            try
            {
                theme = await assets.LoadAsset<ThemeDefinition>(ThemeKey);
                tiers = await theme.LoadTiersAsync(assets);
                Assert.AreEqual(ThemeDefinition.TIER_COUNT, tiers.Count);

                var tier = tiers[TierIndex];
                sprite = await assets.LoadAsset<Sprite>(tier.Sprite);

                var expectedWidth = Mathf.RoundToInt(tier.DiameterUnits * TierDefinition.PixelsPerUnit);
                Assert.IsNotNull(sprite, "The tier sprite did not load.");
                Assert.AreEqual(expectedWidth, Mathf.RoundToInt(sprite.rect.width));
            }
            finally
            {
                assets.ReleaseAsset(sprite);

                if (tiers != null)
                    theme.ReleaseTiers(assets, tiers);

                assets.ReleaseAsset(theme);
            }
        }
    }
}
