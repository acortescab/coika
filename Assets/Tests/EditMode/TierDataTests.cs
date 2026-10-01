using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Acceptance checks of the tier data (issue #2): shipped theme data and the load/release contract.
    /// </summary>
    public class TierDataTests
    {
        private const string ThemePath = "Assets/Data/Themes/Theme_Cosmic.asset";

        private FakeAssetService _assets;
        private ThemeDefinition _theme;

        /// <summary>
        /// Creates a fresh fake asset service for every test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new FakeAssetService();
        }

        /// <summary>
        /// Destroys everything the fake and the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _assets.Cleanup();

            if (_theme != null)
                Object.DestroyImmediate(_theme);
        }

        /// <summary>
        /// The shipped theme has 11 tiers in order and every sprite is as wide as its tier diameter at PPU 16.
        /// </summary>
        [Test]
        public void ShippedTheme_Always_HasElevenTiersWithSpritesMatchingDiameter()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeDefinition>(ThemePath);

            Assert.IsNotNull(theme, $"{ThemePath} not found. Run Coika/Setup Tier Data.");
            var errors = theme.Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        /// <summary>
        /// The theme and every tier are in the Core-Data group, and every tier sprite is in Theme-Cosmic (C-01).
        /// </summary>
        [Test]
        public void ShippedTheme_Always_HasEveryAssetInItsAddressableGroup()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeDefinition>(ThemePath);

            Assert.IsNotNull(theme, $"{ThemePath} not found. Run Coika/Setup Tier Data.");
            var errors = ThemeValidator.GetAddressableErrors(theme);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        /// <summary>
        /// GameConfig points at the shipped theme.
        /// </summary>
        [Test]
        public void GameConfig_Always_ReferencesTheShippedTheme()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig/GameConfig.asset");
            var themeGuid = AssetDatabase.AssetPathToGUID(ThemePath);

            Assert.AreEqual(themeGuid, config.Theme.AssetGUID);
        }

        /// <summary>
        /// Loading the 11 tiers and releasing them leaves no outstanding handles.
        /// </summary>
        [Test]
        public async Task LoadTiersAsync_ThenRelease_LeavesNoOutstandingHandles()
        {
            _theme = CreateThemeWithTierReferences(ThemeDefinition.TIER_COUNT);

            var tiers = await _theme.LoadTiersAsync(_assets);

            Assert.AreEqual(ThemeDefinition.TIER_COUNT, tiers.Count);
            Assert.AreEqual(ThemeDefinition.TIER_COUNT, _assets.OutstandingHandles);

            _theme.ReleaseTiers(_assets, tiers);

            Assert.AreEqual(0, _assets.OutstandingHandles);
        }

        /// <summary>
        /// When a tier fails to load, the tiers loaded before it are released and the error is rethrown.
        /// </summary>
        [Test]
        public void LoadTiersAsync_WhenOneLoadFails_ReleasesTheOnesAlreadyLoaded()
        {
            _theme = CreateThemeWithTierReferences(ThemeDefinition.TIER_COUNT);
            _assets.FailOnAttempt = 5;

            Assert.ThrowsAsync<AssetLoadException>(() => _theme.LoadTiersAsync(_assets));

            Assert.AreEqual(0, _assets.OutstandingHandles);
        }

        /// <summary>
        /// Creates an in-memory theme with the given number of tier references that point at nothing real.
        /// Sets the private tier list by reflection: going through SerializedObject would trigger
        /// ThemeDefinition.OnValidate, which logs errors for tiers that do not exist as assets.
        /// </summary>
        /// <param name="count">Number of tier references to add.</param>
        /// <returns>The theme. The caller must destroy it.</returns>
        private static ThemeDefinition CreateThemeWithTierReferences(int count)
        {
            var theme = ScriptableObject.CreateInstance<ThemeDefinition>();
            var references = new List<AssetReferenceT<TierDefinition>>();

            for (int i = 0; i < count; i++)
                references.Add(new AssetReferenceT<TierDefinition>($"{i:D32}"));

            typeof(ThemeDefinition)
                .GetField("_tiers", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(theme, references);

            return theme;
        }
    }
}
