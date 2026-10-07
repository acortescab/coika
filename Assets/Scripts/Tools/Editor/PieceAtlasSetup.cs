using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Coika.Tools
{
    /// <summary>
    /// Creates and checks the Sprite Atlas of the Theme-Cosmic piece bodies (GDD §10), which packs the bodies folder
    /// only, not the chart icons: padding of at least 4, Tight Packing and rotation off, and uncompressed RGBA32
    /// overrides with Point filtering for Android and iOS, because ASTC smears pixel art.
    /// </summary>
    public static class PieceAtlasSetup
    {
        public const int MIN_PADDING = 4;

        private const int MAX_TEXTURE_SIZE = 2048;

        /// <summary>
        /// Creates the atlas when it is missing, makes sure it packs the bodies folder and sets its packing and
        /// platform settings (padding is set to <see cref="MIN_PADDING"/>). Reimports it only when a setting differs.
        /// </summary>
        public static void Ensure()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PieceArtRules.ATLAS_PATH));
            var asset = File.Exists(PieceArtRules.ATLAS_PATH) ? SpriteAtlasAsset.Load(PieceArtRules.ATLAS_PATH) : new SpriteAtlasAsset();
            if (!PackedPaths().Contains(PieceArtRules.BODIES_FOLDER))
            {
                asset.Add(new[] { AssetDatabase.LoadAssetAtPath<Object>(PieceArtRules.BODIES_FOLDER) });
                SpriteAtlasAsset.Save(asset, PieceArtRules.ATLAS_PATH);
                AssetDatabase.ImportAsset(PieceArtRules.ATLAS_PATH, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(PieceArtRules.ATLAS_PATH);
            if (GetErrors(importer).Count == 0)
            {
                return;
            }

            var packing = importer.packingSettings;
            packing.padding = MIN_PADDING;
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
        /// Lists the ways the atlas differs from the required settings or does not pack the bodies.
        /// </summary>
        /// <param name="importer">The importer of the atlas asset.</param>
        /// <returns>One message per problem; empty when the atlas is correct.</returns>
        public static List<string> GetErrors(SpriteAtlasImporter importer)
        {
            var platforms = SpritePlatformOverrides.Platforms.Select(importer.GetPlatformSettings).ToList();
            var errors = GetSettingsErrors(importer.packingSettings, importer.textureSettings, platforms);
            errors.AddRange(GetPackableErrors(PackedPaths()));
            return errors;
        }

        /// <summary>
        /// Checks the packing, texture and platform settings of an atlas.
        /// </summary>
        /// <param name="packing">The packing settings.</param>
        /// <param name="texture">The texture settings.</param>
        /// <param name="platforms">The settings of the platforms in <see cref="SpritePlatformOverrides.Platforms"/>.</param>
        /// <returns>One message per wrong setting; empty when all are correct.</returns>
        public static List<string> GetSettingsErrors(SpriteAtlasPackingSettings packing, SpriteAtlasTextureSettings texture, IEnumerable<TextureImporterPlatformSettings> platforms)
        {
            var errors = new List<string>();
            var path = PieceArtRules.ATLAS_PATH;

            if (packing.padding < MIN_PADDING)
            {
                errors.Add($"{path}: Padding must be at least {MIN_PADDING}.");
            }

            if (packing.enableTightPacking)
            {
                errors.Add($"{path}: Tight Packing must be off.");
            }

            if (packing.enableRotation)
            {
                errors.Add($"{path}: Rotation must be off.");
            }

            if (texture.filterMode != FilterMode.Point || texture.generateMipMaps)
            {
                errors.Add($"{path}: Filter must be Point with Mip Maps off.");
            }

            foreach (var settings in platforms)
            {
                if (!settings.overridden || settings.format != TextureImporterFormat.RGBA32)
                {
                    errors.Add($"{path}: {settings.name} must override to uncompressed RGBA32.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Checks that the atlas packs the bodies folder.
        /// </summary>
        /// <param name="packedPaths">The project paths of the atlas packables.</param>
        /// <returns>One message when the bodies folder is not packed; empty otherwise.</returns>
        public static List<string> GetPackableErrors(IEnumerable<string> packedPaths)
        {
            var errors = new List<string>();
            if (!packedPaths.Contains(PieceArtRules.BODIES_FOLDER))
            {
                errors.Add($"{PieceArtRules.ATLAS_PATH}: must pack {PieceArtRules.BODIES_FOLDER}.");
            }

            return errors;
        }

        /// <summary>
        /// Lists the project paths of the objects the imported atlas packs.
        /// </summary>
        /// <returns>The asset paths of its packables; empty when the atlas is not imported.</returns>
        private static IEnumerable<string> PackedPaths()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(PieceArtRules.ATLAS_PATH);
            return atlas == null ? Enumerable.Empty<string>() : atlas.GetPackables().Select(AssetDatabase.GetAssetPath);
        }
    }
}
