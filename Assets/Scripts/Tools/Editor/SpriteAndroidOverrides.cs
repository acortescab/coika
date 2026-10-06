using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Pixel quality on Android (GDD §19): every sprite texture gets an Android override with an uncompressed RGBA32
    /// format, so that no block-compression artifact appears on the pixel art. Point filtering is already set on the
    /// importers themselves.
    /// </summary>
    public static class SpriteAndroidOverrides
    {
        private const string ANDROID_PLATFORM = "Android";
        private const string SPRITES_FOLDER = "Assets/Art";

        /// <summary>
        /// Lists the sprite textures whose Android settings are not the uncompressed override.
        /// </summary>
        /// <returns>The asset paths that need <see cref="Apply"/>.</returns>
        public static IReadOnlyList<string> FindNonCompliant()
        {
            return SpriteImporters().Where(importer => !IsCompliant(importer)).Select(importer => importer.assetPath).ToList();
        }

        /// <summary>
        /// Applies the Android override to every sprite texture that does not have it and reimports those textures.
        /// </summary>
        [MenuItem("Tools/Android/Apply Sprite Overrides")]
        public static void Apply()
        {
            var changed = 0;

            foreach (var importer in SpriteImporters().Where(importer => !IsCompliant(importer)))
            {
                var settings = importer.GetPlatformTextureSettings(ANDROID_PLATFORM);
                settings.overridden = true;
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                changed++;
            }

            Debug.Log($"Android sprite overrides applied to {changed} texture(s).");
        }

        /// <summary>
        /// Checks the Android settings of one importer.
        /// </summary>
        /// <param name="importer">A sprite texture importer.</param>
        /// <returns>True when the Android override is uncompressed RGBA32 and the filter is point.</returns>
        private static bool IsCompliant(TextureImporter importer)
        {
            var settings = importer.GetPlatformTextureSettings(ANDROID_PLATFORM);
            return settings.overridden
                && settings.format == TextureImporterFormat.RGBA32
                && importer.filterMode == FilterMode.Point;
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
