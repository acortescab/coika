using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coika.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Generates the flat-colour placeholder circle sprites of the 11 tiers (GDD §10), imports them with the
    /// pixel-art settings, puts them in the Theme-Cosmic Addressable group and assigns them to their TierDefinition.
    /// Idempotent: running it again rewrites nothing that has not changed.
    /// </summary>
    public static class PlaceholderTierSpriteGenerator
    {
        public const string TiersFolder = "Assets/Data/Tiers";
        public const string SpritesFolder = "Assets/Art/Sprites/Tiers";
        public const string SpriteGroupName = "Theme-Cosmic";

        /// <summary>
        /// Generates, imports, registers and assigns the sprite of every tier. Aborts with an error, changing
        /// nothing, when the 11 tiers or the Theme-Cosmic group are missing.
        /// </summary>
        [MenuItem("Coika/Generate Placeholder Tier Sprites")]
        public static void Generate()
        {
            var tiers = FindTiers();
            if (tiers.Count != ThemeDefinition.TIER_COUNT)
            {
                Debug.LogError($"Expected {ThemeDefinition.TIER_COUNT} TierDefinition assets in {TiersFolder}, found {tiers.Count}.");
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings != null ? settings.FindGroup(SpriteGroupName) : null;
            if (group == null)
            {
                Debug.LogError($"Addressables group '{SpriteGroupName}' not found.");
                return;
            }

            Directory.CreateDirectory(SpritesFolder);

            foreach (var tier in tiers)
            {
                var path = $"{SpritesFolder}/{tier.name}.png";
                var diameterPx = Mathf.RoundToInt(tier.DiameterUnits * TierDefinition.PixelsPerUnit);

                WritePngIfChanged(path, DrawCircle(diameterPx, tier.TierColor));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureImporter(path);

                var guid = AssetDatabase.AssetPathToGUID(path);
                settings.CreateOrMoveEntry(guid, group, false, false);
                AssignSprite(tier, guid);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Placeholder tier sprites are up to date in {SpritesFolder}.");
        }

        /// <summary>
        /// Finds every TierDefinition asset under <see cref="TiersFolder"/>.
        /// </summary>
        /// <returns>The tiers sorted by their index.</returns>
        internal static List<TierDefinition> FindTiers()
        {
            return AssetDatabase.FindAssets("t:TierDefinition", new[] { TiersFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TierDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(tier => tier.Index)
                .ToList();
        }

        /// <summary>
        /// Draws a hard-edged circle that touches the canvas edges, with a 1 px darker outline and no
        /// anti-aliasing, to keep the pixel-art look. The output is deterministic for the same input.
        /// </summary>
        /// <param name="size">Side of the square canvas in pixels; equals the circle diameter.</param>
        /// <param name="fillColor">Flat fill colour. The outline is derived from it.</param>
        /// <returns>The image encoded as PNG.</returns>
        private static byte[] DrawCircle(int size, Color fillColor)
        {
            var outlineColor = Color.Lerp(fillColor, Color.black, 0.35f);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));

                    if (distance > radius)
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    else if (distance > radius - 1f)
                        pixels[y * size + x] = outlineColor;
                    else
                        pixels[y * size + x] = fillColor;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        /// <summary>
        /// Writes the PNG only when the file is missing or different, so reruns do not touch unchanged files.
        /// </summary>
        /// <param name="path">Project path of the PNG.</param>
        /// <param name="png">Encoded image to write.</param>
        private static void WritePngIfChanged(string path, byte[] png)
        {
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(png))
                return;

            File.WriteAllBytes(path, png);
        }

        /// <summary>
        /// Applies the pixel-art import settings to a sprite (Point filter, no compression, no mipmaps, PPU 16,
        /// centre pivot). Reimports only when a setting differs.
        /// </summary>
        /// <param name="path">Project path of the PNG.</param>
        private static void ConfigureImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);

            var isConfigured = importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, TierDefinition.PixelsPerUnit)
                && importer.filterMode == FilterMode.Point
                && importer.textureCompression == TextureImporterCompression.Uncompressed
                && !importer.mipmapEnabled
                && importer.alphaIsTransparency
                && textureSettings.spriteAlignment == (int)SpriteAlignment.Center;

            if (isConfigured)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = TierDefinition.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(textureSettings);
            importer.spritePivot = new Vector2(0.5f, 0.5f);

            importer.SaveAndReimport();
        }

        /// <summary>
        /// Points the sprite reference of a tier at the sprite with the given GUID. Does nothing when it already does.
        /// </summary>
        /// <param name="tier">The tier to modify.</param>
        /// <param name="spriteGuid">GUID of the sprite asset.</param>
        private static void AssignSprite(TierDefinition tier, string spriteGuid)
        {
            var serializedTier = new SerializedObject(tier);
            var guidProperty = serializedTier.FindProperty("_sprite.m_AssetGUID");

            if (guidProperty.stringValue == spriteGuid)
                return;

            guidProperty.stringValue = spriteGuid;
            serializedTier.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tier);
        }
    }
}
