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
    /// Checks the particle assets of issue #32: the merge burst counts of <see cref="FeedbackConfig"/> and the shipped
    /// prefab and textures (Fx group, C-01). The asset tests fail until <c>Coika/Setup Particles</c> has been run.
    /// </summary>
    public class FxPrefabTests
    {
        private const int TIER_COUNT = 11;

        /// <summary>
        /// The merge burst grows from the minimum at tier 0 to the maximum at the last tier, always within 6 to 10.
        /// </summary>
        [Test]
        public void MergeBurstCount_AcrossTiers_GrowsWithinTheRange()
        {
            var config = ScriptableObject.CreateInstance<FeedbackConfig>();
            var previous = 0;

            for (var tier = 0; tier < TIER_COUNT; tier++)
            {
                var count = config.MergeBurstCount(tier, TIER_COUNT);
                Assert.GreaterOrEqual(count, 6);
                Assert.LessOrEqual(count, 10);
                Assert.GreaterOrEqual(count, previous, "The burst never shrinks with the tier.");
                previous = count;
            }

            Assert.AreEqual(config.MergeBurstMinCount, config.MergeBurstCount(0, TIER_COUNT));
            Assert.AreEqual(config.MergeBurstMaxCount, config.MergeBurstCount(TIER_COUNT - 1, TIER_COUNT));

            Object.DestroyImmediate(config);
        }

        /// <summary>
        /// The prefab and the two textures are Addressable in the Fx group (C-01).
        /// </summary>
        [TestCase(FxPrefabTool.PrefabPath)]
        [TestCase(FxPrefabTool.PixelTexturePath)]
        [TestCase(FxPrefabTool.RingTexturePath)]
        public void Asset_Shipped_IsInTheFxGroup(string path)
        {
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));

            Assert.IsNotNull(entry, $"{path} must be Addressable. Run Coika/Setup Particles.");
            Assert.AreEqual(FxPrefabTool.FxGroupName, entry.parentGroup.Name);
        }

        /// <summary>
        /// The textures are point-sampled, uncompressed and without mipmaps, so the pixels stay crisp (GDD §10).
        /// </summary>
        [TestCase(FxPrefabTool.PixelTexturePath)]
        [TestCase(FxPrefabTool.RingTexturePath)]
        public void Texture_Shipped_IsPointSampled(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            Assert.IsNotNull(importer, $"{path} is missing. Run Coika/Setup Particles.");
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.IsFalse(importer.mipmapEnabled);
        }

        /// <summary>
        /// The prefab has a spawner with a particle system for the pixels and one for the rings, both emitting nothing
        /// by themselves.
        /// </summary>
        [Test]
        public void Prefab_Shipped_HasASpawnerWithTwoQuietSystems()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FxPrefabTool.PrefabPath);

            Assert.IsNotNull(prefab, "The prefab is missing. Run Coika/Setup Particles.");
            Assert.IsNotNull(prefab.GetComponent<ParticleSpawner>());
            var systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            Assert.AreEqual(2, systems.Length);
            foreach (var system in systems)
            {
                Assert.IsFalse(system.emission.enabled, "The spawner emits every particle.");
                Assert.AreEqual(ParticleSystemSimulationSpace.World, system.main.simulationSpace);
            }
        }
    }
}
