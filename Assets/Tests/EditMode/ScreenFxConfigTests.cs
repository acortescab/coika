using Coika.Data;
using Coika.Fx;
using Coika.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the tuning of the screen effects of issue #33 in <see cref="FeedbackConfig"/> and the shipped overlay
    /// prefab (Fx group, C-01). The asset tests fail until <c>Coika/Setup Screen Effects</c> has been run.
    /// </summary>
    public class ScreenFxConfigTests
    {
        /// <summary>
        /// The merge shake is 0.05 x (tier - 7) from tier 8 up and nothing below.
        /// </summary>
        [Test]
        public void MergeShakeAmplitude_AcrossTiers_FollowsTheFormula()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();

            Assert.AreEqual(0f, config.MergeShakeAmplitude(7));
            Assert.AreEqual(0.05f, config.MergeShakeAmplitude(8), 0.0001f);
            Assert.AreEqual(0.10f, config.MergeShakeAmplitude(9), 0.0001f);
            Assert.AreEqual(0.15f, config.MergeShakeAmplitude(10), 0.0001f);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The defaults are the ones of the issue, and the supernova shakes harder than any merge.
        /// </summary>
        [Test]
        public void Defaults_Always_AreTheValuesOfTheIssue()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();

            Assert.AreEqual(8, config.HeavyMergeMinTier);
            Assert.AreEqual(0.2f, config.ShakeDuration, 0.0001f);
            Assert.AreEqual(0.7f, config.SlowMoScale, 0.0001f);
            Assert.AreEqual(0.1f, config.SlowMoDuration, 0.0001f);
            Assert.AreEqual(0.15f, config.ScreenFlashDuration, 0.0001f);
            Assert.AreEqual(3f, config.MaxFlashesPerSecond, 0.0001f);
            Assert.Greater(config.SupernovaShakeAmplitude, config.MergeShakeAmplitude(10));
            Assert.LessOrEqual(config.SupernovaShakeAmplitude, config.ShakeMaxAmplitude);

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The overlay prefab is Addressable in the Fx group (C-01).
        /// </summary>
        [Test]
        public void Prefab_Shipped_IsInTheFxGroup()
        {
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(ScreenFxPrefabTool.PREFAB_PATH));

            Assert.IsNotNull(entry, "The prefab must be Addressable. Run Coika/Setup Screen Effects.");
            Assert.AreEqual(FxPrefabTool.FX_GROUP_NAME, entry.parentGroup.Name);
        }

        /// <summary>
        /// The overlay prefab is a Screen Space Overlay canvas with a flash component, and does not catch input.
        /// </summary>
        [Test]
        public void Prefab_Shipped_IsAnOverlayThatDoesNotBlockInput()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenFxPrefabTool.PREFAB_PATH);

            Assert.IsNotNull(prefab, "Run Coika/Setup Screen Effects.");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, prefab.GetComponent<Canvas>().renderMode);
            Assert.IsNotNull(prefab.GetComponent<ScreenFlash>());
            var group = prefab.GetComponent<CanvasGroup>();
            Assert.IsFalse(group.blocksRaycasts);
            Assert.AreEqual(0f, group.alpha);
        }
    }
}
