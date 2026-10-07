using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coika.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Checks the piece art against GDD §10 and constraint C-01: one body and one chart icon per tier with the right
    /// names, the import settings of every texture, the palette, the atlas settings, the Theme-Cosmic group of every
    /// entry and that each collider matches the visible body to within <see cref="COLLIDER_TOLERANCE_PX"/> pixels.
    /// </summary>
    public static class PieceArtValidator
    {
        public const float COLLIDER_TOLERANCE_PX = 0.5f;

        /// <summary>
        /// Logs OK or one error per problem.
        /// </summary>
        [MenuItem("Coika/Validate Piece Art")]
        public static void ValidatePieceArt()
        {
            var errors = GetErrors();
            if (errors.Count == 0)
            {
                Debug.Log("Piece art validation OK.");
                return;
            }

            foreach (var error in errors)
            {
                Debug.LogError(error);
            }
        }

        /// <summary>
        /// Collects every problem of the piece art.
        /// </summary>
        /// <returns>One message per problem; empty when all is correct.</returns>
        public static List<string> GetErrors()
        {
            var errors = new List<string>();
            var tiers = PieceArtRules.FindTiers();
            if (tiers.Count != ThemeDefinition.TIER_COUNT)
            {
                errors.Add($"Expected {ThemeDefinition.TIER_COUNT} tiers in {PieceArtRules.TiersFolder}, found {tiers.Count}.");
                return errors;
            }

            var paths = new List<string>();
            foreach (var tier in tiers)
            {
                var bodyPath = $"{PieceArtRules.BodiesFolder}/{PieceArtRules.BodyName(tier)}.png";
                var iconPath = $"{PieceArtRules.IconsFolder}/{PieceArtRules.IconName(tier)}.png";
                CheckTier(tier, bodyPath, iconPath, errors);
                paths.Add(bodyPath);
                paths.Add(iconPath);
            }

            CheckStrays(paths, errors);
            CheckPalette(paths.Where(File.Exists).ToList(), errors);
            CheckAtlasAndGroups(paths, errors);
            return errors;
        }

        /// <summary>
        /// Counts the opaque pixels of a PNG file along its width: the measure of the visible body.
        /// </summary>
        /// <param name="path">Project path of the PNG.</param>
        /// <returns>The width in pixels of the bounding box of the pixels with alpha above zero, or 0 when there are none.</returns>
        public static int VisibleWidth(string path)
        {
            var texture = LoadPng(path);
            try
            {
                var pixels = texture.GetPixels32();
                var minX = int.MaxValue;
                var maxX = -1;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a == 0)
                    {
                        continue;
                    }

                    var x = i % texture.width;
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                }

                return maxX < 0 ? 0 : maxX - minX + 1;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Checks the files, settings and collider of one tier.
        /// </summary>
        /// <param name="tier">The tier.</param>
        /// <param name="bodyPath">Expected path of the body sprite.</param>
        /// <param name="iconPath">Expected path of the chart icon.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckTier(TierDefinition tier, string bodyPath, string iconPath, List<string> errors)
        {
            foreach (var path in new[] { bodyPath, iconPath })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    errors.Add($"{path}: missing.");
                    continue;
                }

                errors.AddRange(PieceArtRules.GetImporterErrors(importer));
            }

            if (AssetDatabase.GUIDToAssetPath(tier.Sprite.AssetGUID) != bodyPath)
            {
                errors.Add($"{tier.name}: the sprite reference must point at {bodyPath}.");
            }

            if (File.Exists(bodyPath))
            {
                var bodyRadius = VisibleWidth(bodyPath) * 0.5f;
                var colliderRadius = tier.DiameterUnits * TierDefinition.PixelsPerUnit * 0.5f;
                if (Math.Abs(bodyRadius - colliderRadius) > COLLIDER_TOLERANCE_PX)
                {
                    errors.Add($"{bodyPath}: visible radius {bodyRadius} px differs from the collider radius {colliderRadius} px.");
                }
            }

            if (File.Exists(iconPath) && VisibleWidth(iconPath) != PieceArtRules.IconSize)
            {
                errors.Add($"{iconPath}: the icon must be {PieceArtRules.IconSize} px wide.");
            }
        }

        /// <summary>
        /// Reports piece art files that belong to no tier, such as a left-over placeholder.
        /// </summary>
        /// <param name="expected">The paths of the bodies and icons of the 11 tiers.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckStrays(List<string> expected, List<string> errors)
        {
            var found = AssetDatabase.FindAssets("t:Texture2D", new[] { PieceArtRules.BodiesFolder, PieceArtRules.IconsFolder })
                .Select(AssetDatabase.GUIDToAssetPath);

            foreach (var path in found.Where(path => !expected.Contains(path)))
            {
                errors.Add($"{path}: not a piece_XX_name or icon_XX_name file of a tier.");
            }
        }

        /// <summary>
        /// Checks that all textures together use at most <see cref="CoikaPalette.MAX_COLORS"/> opaque colours, all of
        /// them from the palette, and that the palette file lists the same colours.
        /// </summary>
        /// <param name="paths">The PNG paths to read.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckPalette(List<string> paths, List<string> errors)
        {
            var used = new HashSet<Color32>();
            foreach (var path in paths)
            {
                var texture = LoadPng(path);
                foreach (var pixel in texture.GetPixels32().Where(pixel => pixel.a == 255))
                {
                    used.Add(pixel);
                }

                UnityEngine.Object.DestroyImmediate(texture);
            }

            if (used.Count > CoikaPalette.MAX_COLORS)
            {
                errors.Add($"The piece art uses {used.Count} colours; the maximum is {CoikaPalette.MAX_COLORS}.");
            }

            var palette = CoikaPalette.Colors();
            foreach (var color in used.Where(color => !palette.Contains(color)))
            {
                errors.Add($"The piece art uses {CoikaPalette.ToHex(color)}, which is not in the palette.");
            }

            if (!File.Exists(CoikaPalette.GplPath) || File.ReadAllText(CoikaPalette.GplPath) != CoikaPalette.ToGpl())
            {
                errors.Add($"{CoikaPalette.GplPath} is missing or differs from the palette in code.");
            }
        }

        /// <summary>
        /// Checks the atlas settings and that every sprite, icon and the atlas is in the Theme-Cosmic group.
        /// </summary>
        /// <param name="paths">The paths of the bodies and icons.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckAtlasAndGroups(List<string> paths, List<string> errors)
        {
            if (AssetImporter.GetAtPath(PieceArtRules.AtlasPath) is SpriteAtlasImporter atlas)
            {
                errors.AddRange(PieceAtlasSetup.GetErrors(atlas));
            }
            else
            {
                errors.Add($"{PieceArtRules.AtlasPath}: missing.");
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("Addressables settings not found.");
                return;
            }

            foreach (var path in paths.Append(PieceArtRules.AtlasPath))
            {
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                if (entry == null || entry.parentGroup.Name != PieceArtRules.GroupName)
                {
                    errors.Add($"{path}: must be an Addressable entry of the '{PieceArtRules.GroupName}' group.");
                }
            }
        }

        /// <summary>
        /// Decodes a PNG file into a readable texture.
        /// </summary>
        /// <param name="path">Project path of the PNG.</param>
        /// <returns>The texture; the caller destroys it.</returns>
        private static Texture2D LoadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }
    }
}
