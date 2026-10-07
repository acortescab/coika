using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Coika.Data;
using UnityEditor;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// The rules of the piece art (GDD §10): where the files live, how they are named and which import settings they
    /// need. The postprocessor applies them on import and the validator checks them.
    /// </summary>
    public static class PieceArtRules
    {
        public const string TIERS_FOLDER = "Assets/Data/Tiers";
        public const string BODIES_FOLDER = "Assets/Art/Sprites/Tiers";
        public const string ICONS_FOLDER = "Assets/Art/Sprites/Icons";
        public const string ATLAS_PATH = "Assets/Art/Atlases/Theme-Cosmic.spriteatlasv2";
        public const string GROUP_NAME = "Theme-Cosmic";
        public const int ICON_SIZE = 12;

        private const string BODY_PREFIX = "piece";
        private const string ICON_PREFIX = "icon";

        private static readonly Regex ArtName = new Regex(@"^(piece|icon)_(\d\d)_[a-z0-9_]+\.png$");
        private static readonly Regex WordBoundary = new Regex(@"(?<=[a-z0-9])(?=[A-Z])");

        /// <summary>
        /// Finds every TierDefinition asset under <see cref="TIERS_FOLDER"/>.
        /// </summary>
        /// <returns>The tiers sorted by their index.</returns>
        public static List<TierDefinition> FindTiers()
        {
            return AssetDatabase.FindAssets("t:TierDefinition", new[] { TIERS_FOLDER })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TierDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(tier => tier.Index)
                .ToList();
        }

        /// <summary>
        /// Builds the file name of the body sprite of a tier, such as piece_04_dwarf_planet.
        /// </summary>
        /// <param name="tier">The tier, whose asset name looks like Tier_04_DwarfPlanet.</param>
        /// <returns>The sprite name without extension.</returns>
        public static string BodyName(TierDefinition tier)
        {
            return $"{BODY_PREFIX}_{tier.Index:00}_{SnakeCase(tier)}";
        }

        /// <summary>
        /// Builds the file name of the chart icon of a tier, such as icon_04_dwarf_planet.
        /// </summary>
        /// <param name="tier">The tier, whose asset name looks like Tier_04_DwarfPlanet.</param>
        /// <returns>The icon name without extension.</returns>
        public static string IconName(TierDefinition tier)
        {
            return $"{ICON_PREFIX}_{tier.Index:00}_{SnakeCase(tier)}";
        }

        /// <summary>
        /// Tells whether a path is a piece body or a chart icon, by its file name.
        /// </summary>
        /// <param name="assetPath">Project path of an asset.</param>
        /// <returns>True for names like piece_XX_name.png and icon_XX_name.png under Assets/Art.</returns>
        public static bool IsPieceArt(string assetPath)
        {
            return assetPath.StartsWith("Assets/Art/") && ArtName.IsMatch(System.IO.Path.GetFileName(assetPath));
        }

        /// <summary>
        /// Applies the required import settings to an importer, without reimporting it: sprite, single, 16 pixels per
        /// unit, point filter, no compression, no mip maps, alpha is transparency, centre pivot and the mobile overrides.
        /// </summary>
        /// <param name="importer">The importer of a piece body or chart icon.</param>
        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TierDefinition.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(textureSettings);
            importer.spritePivot = new Vector2(0.5f, 0.5f);

            SpritePlatformOverrides.Configure(importer);
        }

        /// <summary>
        /// Lists the ways an importer differs from the required settings.
        /// </summary>
        /// <param name="importer">The importer of a piece body or chart icon.</param>
        /// <returns>One message per wrong setting; empty when the importer is correct.</returns>
        public static List<string> GetImporterErrors(TextureImporter importer)
        {
            var errors = new List<string>();
            var path = importer.assetPath;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);

            if (importer.textureType != TextureImporterType.Sprite)
            {
                errors.Add($"{path}: Texture Type must be Sprite.");
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                errors.Add($"{path}: Sprite Mode must be Single.");
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, TierDefinition.PixelsPerUnit))
            {
                errors.Add($"{path}: Pixels Per Unit must be {TierDefinition.PixelsPerUnit}.");
            }

            if (importer.filterMode != FilterMode.Point)
            {
                errors.Add($"{path}: Filter Mode must be Point.");
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                errors.Add($"{path}: Compression must be None.");
            }

            if (importer.mipmapEnabled)
            {
                errors.Add($"{path}: Mip Maps must be off.");
            }

            if (textureSettings.spriteAlignment != (int)SpriteAlignment.Center)
            {
                errors.Add($"{path}: Pivot must be Center.");
            }

            if (!SpritePlatformOverrides.IsCompliant(importer))
            {
                errors.Add($"{path}: Android and iOS must override to uncompressed RGBA32.");
            }

            return errors;
        }

        /// <summary>
        /// Turns the name part of a tier asset (Tier_04_DwarfPlanet) into lower-case words joined by underscores.
        /// </summary>
        /// <param name="tier">The tier.</param>
        /// <returns>For example dwarf_planet.</returns>
        private static string SnakeCase(TierDefinition tier)
        {
            var name = tier.name.Substring(tier.name.LastIndexOf('_') + 1);
            return WordBoundary.Replace(name, "_").ToLowerInvariant();
        }
    }
}
