using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Coika.Tools
{
    /// <summary>
    /// Creates and checks the Sprite Atlas of the Theme-Cosmic piece bodies (GDD §10): padding of at least 4,
    /// Tight Packing off, and uncompressed RGBA32 overrides with Point filtering for Android and iOS, because ASTC
    /// smears pixel art.
    /// </summary>
    public static class PieceAtlasSetup
    {
        public const int MIN_PADDING = 4;

        private const int PADDING = 4;
        private const int MAX_TEXTURE_SIZE = 2048;

        /// <summary>
        /// Creates the atlas when it is missing and sets its packing and platform settings. Reimports it only when a
        /// setting differs.
        /// </summary>
        public static void Ensure()
        {
            if (!File.Exists(PieceArtRules.AtlasPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PieceArtRules.AtlasPath));
                var asset = new SpriteAtlasAsset();
                asset.Add(new[] { AssetDatabase.LoadAssetAtPath<Object>(PieceArtRules.BodiesFolder) });
                SpriteAtlasAsset.Save(asset, PieceArtRules.AtlasPath);
                AssetDatabase.ImportAsset(PieceArtRules.AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(PieceArtRules.AtlasPath);
            if (GetErrors(importer).Count == 0)
            {
                return;
            }

            var packing = importer.packingSettings;
            packing.padding = PADDING;
            packing.enableTightPacking = false;
            packing.enableRotation = false;
            importer.packingSettings = packing;

            var texture = importer.textureSettings;
            texture.filterMode = FilterMode.Point;
            texture.generateMipMaps = false;
            importer.textureSettings = texture;

            foreach (var platform in SpritePlatformOverrides.Platforms)
            {
                importer.SetPlatformSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = MAX_TEXTURE_SIZE,
                    format = TextureImporterFormat.RGBA32,
                    textureCompression = TextureImporterCompression.Uncompressed,
                });
            }

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Lists the ways the atlas differs from the required settings.
        /// </summary>
        /// <param name="importer">The importer of the atlas asset.</param>
        /// <returns>One message per wrong setting; empty when the atlas is correct.</returns>
        public static System.Collections.Generic.List<string> GetErrors(SpriteAtlasImporter importer)
        {
            var errors = new System.Collections.Generic.List<string>();
            var path = PieceArtRules.AtlasPath;

            if (importer.packingSettings.padding < MIN_PADDING)
            {
                errors.Add($"{path}: Padding must be at least {MIN_PADDING}.");
            }

            if (importer.packingSettings.enableTightPacking)
            {
                errors.Add($"{path}: Tight Packing must be off.");
            }

            if (importer.textureSettings.filterMode != FilterMode.Point || importer.textureSettings.generateMipMaps)
            {
                errors.Add($"{path}: Filter must be Point with Mip Maps off.");
            }

            foreach (var platform in SpritePlatformOverrides.Platforms)
            {
                var settings = importer.GetPlatformSettings(platform);
                if (!settings.overridden || settings.format != TextureImporterFormat.RGBA32)
                {
                    errors.Add($"{path}: {platform} must override to uncompressed RGBA32.");
                }
            }

            return errors;
        }
    }
}
