using System;
using System.Text.RegularExpressions;
using Coika.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks how <see cref="GameConfig"/> produces and validates its spawn values (issue #5): the defaults, the
    /// errors logged in the Editor, the error thrown when a run would start with bad values, and the shipped asset.
    /// </summary>
    public class GameConfigSpawnTests
    {
        private const string GAME_CONFIG_PATH = "Assets/Data/GameConfig/GameConfig.asset";

        private GameConfig _config;

        /// <summary>
        /// Destroys the config a test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
            }
        }

        /// <summary>
        /// A new config holds the GDD spawn values.
        /// </summary>
        [Test]
        public void CreateSpawnSettings_WithTheDefaultConfig_ReturnsTheGddValues()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();

            var settings = _config.CreateSpawnSettings();

            AssertGddValues(settings);
        }

        /// <summary>
        /// A config with a weights array that does not match the tier count logs the problem when edited and throws
        /// a clear error when the settings are created.
        /// </summary>
        [Test]
        public void CreateSpawnSettings_WithAMismatchedWeightsLength_LogsAndThrowsAClearError()
        {
            LogAssert.Expect(LogType.Error, new Regex("3 spawn weights but 5 spawnable tiers"));
            _config = TestGameConfig.CreateWithSpawn(new[] { 30f, 28f, 20f }, 5, 3, new[] { 0, 1, 0 });

            var exception = Assert.Throws<ArgumentException>(() => _config.CreateSpawnSettings());

            StringAssert.Contains("3 spawn weights but 5 spawnable tiers", exception.Message);
        }

        /// <summary>
        /// Editing the config with a forced opening that breaks the anti-streak rule logs an error.
        /// </summary>
        [Test]
        public void OnValidate_WithAForcedOpeningBreakingTheStreak_LogsAnError()
        {
            LogAssert.Expect(LogType.Error, new Regex("repeats tier 2 more than 3 times in a row"));

            _config = TestGameConfig.CreateWithSpawn(new[] { 30f, 28f, 20f, 14f, 8f }, 5, 3, new[] { 2, 2, 2, 2 });
        }

        /// <summary>
        /// The asset that ships with the project is valid and holds the migrated forced opening 0, 1, 0.
        /// </summary>
        [Test]
        public void ShippedGameConfig_Always_IsValidAndHasTheForcedOpening()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>(GAME_CONFIG_PATH);
            Assert.IsNotNull(shipped, $"{GAME_CONFIG_PATH} not found.");

            AssertGddValues(shipped.CreateSpawnSettings());
        }

        /// <summary>
        /// Checks the GDD defaults: weights 30/28/20/14/8, five tiers, at most three in a row, opening 0, 1, 0.
        /// </summary>
        /// <param name="settings">The settings to check.</param>
        private static void AssertGddValues(SpawnSettings settings)
        {
            var weights = new[] { 30f, 28f, 20f, 14f, 8f };

            Assert.AreEqual(5, settings.TierCount);
            Assert.AreEqual(3, settings.AntiStreakMax);
            for (int tier = 0; tier < weights.Length; tier++)
            {
                Assert.AreEqual(weights[tier], settings.GetWeight(tier), 0.0001f, $"weight of tier {tier}");
            }

            Assert.AreEqual(3, settings.ForcedOpeningLength);
            Assert.AreEqual(0, settings.GetForcedTier(0));
            Assert.AreEqual(1, settings.GetForcedTier(1));
            Assert.AreEqual(0, settings.GetForcedTier(2));
        }
    }
}
