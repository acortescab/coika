using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Pixel quality on mobile (GDD §19): every sprite texture gets an Android and an iOS override with an
    /// uncompressed RGBA32 format, so that no block-compression artifact (ASTC smears pixel art) appears. Point
    /// filtering is set on the importers themselves.
    /// </summary>
    public static class SpritePlatformOverrides
    {
        /// <summary>
        /// The platforms that need the uncompressed override.
        /// </summary>
        public static readonly string[] Platforms = { "Android", "iOS" };

        private const string SPRITES_FOLDER = "Assets/Art";

        /// <summary>
        /// Lists the sprite textures whose mobile settings are not the uncompressed override.
        /// </summary>
        /// <returns>The asset paths that need <see cref="Apply"/>.</returns>
        public static IReadOnlyList<string> FindNonCompliant()
        {
            return SpriteImporters().Where(importer => !IsCompliant(importer)).Select(importer => importer.assetPath).ToList();
        }

        /// <summary>
        /// Applies the mobile overrides to every sprite texture that does not have them and reimports those textures.
        /// </summary>
        [MenuItem("Tools/Android/Apply Sprite Overrides")]
        public static void Apply()
        {
            var changed = 0;

            foreach (var importer in SpriteImporters().Where(importer => !IsCompliant(importer)))
            {
                Configure(importer);
                importer.SaveAndReimport();
                changed++;
            }

            Debug.Log($"Mobile sprite overrides applied to {changed} texture(s).");
        }

        /// <summary>
        /// Sets the uncompressed RGBA32 override of every platform in <see cref="Platforms"/> on an importer, without
        /// reimporting it.
        /// </summary>
        /// <param name="importer">A sprite texture importer.</param>
        public static void Configure(TextureImporter importer)
        {
            foreach (var platform in Platforms)
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        /// <summary>
        /// Checks the mobile settings of one importer.
        /// </summary>
        /// <param name="importer">A sprite texture importer.</param>
        /// <returns>True when every platform override is uncompressed RGBA32 and the filter is point.</returns>
        public static bool IsCompliant(TextureImporter importer)
        {
            return importer.filterMode == FilterMode.Point
                && Platforms.All(platform =>
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    return settings.overridden && settings.format == TextureImporterFormat.RGBA32;
                });
        }

        /// <summary>
        /// Finds the importers of all sprite textures under <see cref="SPRITES_FOLDER"/>.
        /// </summary>
        /// <returns>The texture importers of type Sprite.</returns>
        private static IEnumerable<TextureImporter> SpriteImporters()
        {
            return AssetDatabase.FindAssets("t:Texture2D", new[] { SPRITES_FOLDER })
                .Select(guid => AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter)
                .Where(importer => importer != null && importer.textureType == TextureImporterType.Sprite);
        }
    }
}
