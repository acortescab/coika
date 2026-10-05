using Coika.Data;
using Coika.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="FeedbackConfig"/> (issue #31): the GDD §9 values, the landing amplitude and the shipped
    /// asset (Core-Data group, wired into the GameConfig). The asset tests fail until <c>Coika/Setup Feedback Config</c>
    /// has been run.
    /// </summary>
    public class FeedbackConfigTests
    {
        private const float TOLERANCE = 0.0001f;
        private const string GAME_CONFIG_PATH = "Assets/Data/GameConfig/GameConfig.asset";

        /// <summary>
        /// The defaults are the durations of the GDD §9.
        /// </summary>
        [Test]
        public void Defaults_Always_AreTheGddDurations()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();

            Assert.AreEqual(0.15f, config.SpawnDuration, TOLERANCE);
            Assert.AreEqual(0.1f, config.LandDuration, TOLERANCE);
            Assert.AreEqual(0.2f, config.MergePopDuration, TOLERANCE);
            Assert.AreEqual(1.2f, config.MergePopPeak, TOLERANCE);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The landing amplitude is 0 below the threshold, proportional above it and clamped.
        /// </summary>
        [Test]
        public void LandAmplitude_WithGrowingImpulse_IsZeroThenProportionalThenClamped()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();
            var threshold = config.LandImpulseThreshold;

            Assert.AreEqual(0f, config.LandAmplitude(threshold - 0.01f));
            Assert.AreEqual(threshold * config.LandAmplitudePerImpulse, config.LandAmplitude(threshold), TOLERANCE);
            Assert.AreEqual(config.LandAmplitudeMax, config.LandAmplitude(10000f), TOLERANCE);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The shipped asset is in the Core-Data group and the shipped GameConfig references it (C-01).
        /// </summary>
        [Test]
        public void FeedbackConfig_Shipped_IsInCoreDataAndWiredIntoTheGameConfig()
        {
            var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(GAME_CONFIG_PATH);
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(
                AssetDatabase.AssetPathToGUID(FeedbackConfigSetup.FEEDBACK_CONFIG_PATH));

            Assert.IsNotNull(entry, "FeedbackConfig must be Addressable. Run Coika/Setup Feedback Config.");
            Assert.AreEqual(TierDataSetup.DataGroupName, entry.parentGroup.Name);
            Assert.IsNotNull(gameConfig.Feedback, "GameConfig.Feedback is not assigned.");
            Assert.AreEqual(FeedbackConfigSetup.FEEDBACK_CONFIG_PATH, AssetDatabase.GetAssetPath(gameConfig.Feedback));
        }
    }
}
