using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
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

            Assert.AreEqual(0.15f, config.Animations.SpawnDuration, TOLERANCE);
            Assert.AreEqual(0.1f, config.Animations.LandDuration, TOLERANCE);
            Assert.AreEqual(0.2f, config.Animations.MergePopDuration, TOLERANCE);
            Assert.AreEqual(1.2f, config.Animations.MergePopPeak, TOLERANCE);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The landing amplitude is 0 below the threshold, proportional above it and clamped.
        /// </summary>
        [Test]
        public void LandAmplitude_WithGrowingImpulse_IsZeroThenProportionalThenClamped()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();
            var threshold = config.Animations.LandImpulseThreshold;

            Assert.AreEqual(0f, config.Animations.LandAmplitude(threshold - 0.01f));
            Assert.AreEqual(threshold * config.Animations.LandAmplitudePerImpulse, config.Animations.LandAmplitude(threshold), TOLERANCE);
            Assert.AreEqual(config.Animations.LandAmplitudeMax, config.Animations.LandAmplitude(10000f), TOLERANCE);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// A merge buzzes Medium below the heavy haptic tier and Heavy from it up.
        /// </summary>
        [Test]
        public void MergeHaptic_AroundTheThreshold_IsMediumThenHeavy()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();

            Assert.AreEqual(7, config.Sound.HeavyMergeHapticMinTier);
            Assert.AreEqual(HapticKind.Medium, config.Sound.MergeHaptic(0));
            Assert.AreEqual(HapticKind.Medium, config.Sound.MergeHaptic(6));
            Assert.AreEqual(HapticKind.Heavy, config.Sound.MergeHaptic(7));
            Assert.AreEqual(HapticKind.Heavy, config.Sound.MergeHaptic(10));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The landing volume grows with the impulse between its minimum and 1.
        /// </summary>
        [Test]
        public void LandVolume_WithGrowingImpulse_GrowsBetweenTheMinimumAndOne()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();

            Assert.AreEqual(config.Sound.LandVolumeMin, config.Sound.LandVolume(0f), TOLERANCE);
            Assert.AreEqual(0.5f, config.Sound.LandVolume(0.5f / config.Sound.LandVolumePerImpulse), TOLERANCE);
            Assert.AreEqual(1f, config.Sound.LandVolume(10000f), TOLERANCE);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The landing pitch is inversely proportional to the radius, between its limits.
        /// </summary>
        [Test]
        public void LandPitch_WithGrowingRadius_FallsInverselyBetweenTheLimits()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();
            var reference = config.Sound.LandPitchReferenceRadius;

            Assert.AreEqual(1f, config.Sound.LandPitch(reference), TOLERANCE);
            Assert.AreEqual(1.25f, config.Sound.LandPitch(reference / 1.25f), TOLERANCE);
            Assert.AreEqual(config.Sound.LandPitchMax, config.Sound.LandPitch(0.0001f), TOLERANCE);
            Assert.AreEqual(config.Sound.LandPitchMin, config.Sound.LandPitch(1000f), TOLERANCE);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The game-over flash starts at once for a piece at the top and takes the whole sweep duration to reach the
        /// floor, linearly in between and clamped outside the jar.
        /// </summary>
        [Test]
        public void GameOverDelay_WithGrowingDepth_GrowsFromZeroToTheSweepDuration()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();
            var duration = config.Animations.GameOverSweepDuration;

            Assert.AreEqual(0f, config.Animations.GameOverDelay(10f, 10f, 0f), TOLERANCE);
            Assert.AreEqual(duration * 0.5f, config.Animations.GameOverDelay(5f, 10f, 0f), TOLERANCE);
            Assert.AreEqual(duration, config.Animations.GameOverDelay(0f, 10f, 0f), TOLERANCE);
            Assert.AreEqual(0f, config.Animations.GameOverDelay(12f, 10f, 0f), TOLERANCE, "Above the top is clamped.");
            Assert.AreEqual(duration, config.Animations.GameOverDelay(-3f, 10f, 0f), TOLERANCE, "Below the floor is clamped.");

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The game-over flash leaves the colour alone during its delay, peaks half way through its own time, ends,
        /// and never changes the scale.
        /// </summary>
        [Test]
        public void GameOverFlashEffect_AfterADelay_TintsThenEnds()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();
            var effect = new GameOverFlashEffect();
            const float delay = 0.3f;
            var flash = config.Animations.GameOverFlashDuration;

            effect.Start(config, delay);
            Assert.IsTrue(effect.IsActive);
            Assert.AreEqual(Color.white, effect.Tint(config, null), "Untinted during the delay.");

            effect.Advance(delay + flash * 0.5f);
            Assert.AreEqual(config.Animations.GameOverFlashColor, effect.Tint(config, null), "Peak tint.");
            Assert.AreEqual(Vector2.one, effect.Evaluate(config, null), "The scale is untouched.");

            effect.Advance(flash);
            Assert.IsFalse(effect.IsActive);

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
