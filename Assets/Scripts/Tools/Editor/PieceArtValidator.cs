using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coika.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.U2D;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Checks the piece art against GDD §10 and constraint C-01: one body and one chart icon per tier with the right
    /// names, the import settings of every texture, the palette, the atlas, the Theme-Cosmic group of every entry
    /// and that each collider matches the visible body to within <see cref="COLLIDER_TOLERANCE_PX"/> pixels. The
    /// checks that need no files are public and take plain data, so tests can feed them bad data.
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
                errors.Add($"Expected {ThemeDefinition.TIER_COUNT} tiers in {PieceArtRules.TIERS_FOLDER}, found {tiers.Count}.");
                return errors;
            }

            var paths = new List<string>();
            foreach (var tier in tiers)
            {
                var bodyPath = $"{PieceArtRules.BODIES_FOLDER}/{PieceArtRules.BodyName(tier)}.png";
                var iconPath = $"{PieceArtRules.ICONS_FOLDER}/{PieceArtRules.IconName(tier)}.png";
                CheckTier(tier, bodyPath, iconPath, errors);
                paths.Add(bodyPath);
                paths.Add(iconPath);
            }

            var found = AssetDatabase.FindAssets("t:Texture2D", new[] { PieceArtRules.BODIES_FOLDER, PieceArtRules.ICONS_FOLDER })
                .Select(AssetDatabase.GUIDToAssetPath);
            errors.AddRange(GetStrayErrors(found, paths));
            CheckPalette(tiers, paths.Where(File.Exists).ToList(), errors);
            CheckAtlasAndGroups(paths, errors);
            return errors;
        }

        /// <summary>
        /// Checks that a body is as wide as its collider, so the collider matches the visible body (outline included).
        /// </summary>
        /// <param name="path">Project path of the body sprite, for the message.</param>
        /// <param name="visibleWidth">The visible width in pixels, from <see cref="VisibleWidth"/>.</param>
        /// <param name="diameterUnits">The tier diameter in world units.</param>
        /// <returns>One message when the radii differ by more than <see cref="COLLIDER_TOLERANCE_PX"/>; empty otherwise.</returns>
        public static List<string> GetColliderErrors(string path, int visibleWidth, float diameterUnits)
        {
            var errors = new List<string>();
            var bodyRadius = visibleWidth * 0.5f;
            var colliderRadius = diameterUnits * TierDefinition.PixelsPerUnit * 0.5f;
            if (Math.Abs(bodyRadius - colliderRadius) > COLLIDER_TOLERANCE_PX)
            {
                errors.Add($"{path}: visible radius {bodyRadius} px differs from the collider radius {colliderRadius} px.");
            }

            return errors;
        }

        /// <summary>
        /// Reports piece art files that belong to no tier, such as a left-over placeholder.
        /// </summary>
        /// <param name="found">The texture paths found in the piece art folders.</param>
        /// <param name="expected">The paths of the bodies and icons of the 11 tiers.</param>
        /// <returns>One message per stray file; empty when there are none.</returns>
        public static List<string> GetStrayErrors(IEnumerable<string> found, ICollection<string> expected)
        {
            return found.Where(path => !expected.Contains(path))
                .Select(path => $"{path}: not a piece_XX_name or icon_XX_name file of a tier.")
                .ToList();
        }

        /// <summary>
        /// Checks the colours of all piece art together: at most <see cref="CoikaPalette.MAX_COLORS"/> fully opaque
        /// colours, all from the palette, every tier colour present, and no semi-transparent (anti-aliased) pixel.
        /// </summary>
        /// <param name="opaqueColors">The distinct colours of the pixels with alpha 255.</param>
        /// <param name="partialAlphaPixels">How many pixels have an alpha above 0 and below 255.</param>
        /// <param name="tierColors">The tier colours that must appear.</param>
        /// <returns>One message per problem; empty when all is correct.</returns>
        public static List<string> GetPaletteErrors(ICollection<Color32> opaqueColors, int partialAlphaPixels, IEnumerable<Color32> tierColors)
        {
            var errors = new List<string>();
            if (opaqueColors.Count > CoikaPalette.MAX_COLORS)
            {
                errors.Add($"The piece art uses {opaqueColors.Count} colours; the maximum is {CoikaPalette.MAX_COLORS}.");
            }

            var palette = CoikaPalette.Colors();
            errors.AddRange(opaqueColors.Where(color => !palette.Contains(color))
                .Select(color => $"The piece art uses {CoikaPalette.ToHex(color)}, which is not in the palette."));
            errors.AddRange(tierColors.Where(color => !opaqueColors.Contains(color))
                .Select(color => $"The tier colour {CoikaPalette.ToHex(color)} does not appear in the piece art."));

            if (partialAlphaPixels > 0)
            {
                errors.Add($"{partialAlphaPixels} pixels are semi-transparent; the art must have no anti-aliasing.");
            }

            return errors;
        }

        /// <summary>
        /// Checks that an asset is an Addressable entry of the Theme-Cosmic group.
        /// </summary>
        /// <param name="path">Project path of the asset, for the message.</param>
        /// <param name="groupName">The name of the group of its entry, or null when it has no entry.</param>
        /// <returns>One message when the group is not Theme-Cosmic; empty otherwise.</returns>
        public static List<string> GetGroupErrors(string path, string groupName)
        {
            var errors = new List<string>();
            if (groupName != PieceArtRules.GROUP_NAME)
            {
                errors.Add($"{path}: must be an Addressable entry of the '{PieceArtRules.GROUP_NAME}' group.");
            }

            return errors;
        }

        /// <summary>
        /// Measures the horizontal extent of the visible body: the width from the leftmost to the rightmost pixel
        /// with an alpha above zero.
        /// </summary>
        /// <param name="path">Project path of the PNG.</param>
        /// <returns>The width in pixels, or 0 when no pixel is visible.</returns>
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
        /// Checks the files, import settings, tier sprite reference and collider of one tier.
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
                errors.AddRange(GetColliderErrors(bodyPath, VisibleWidth(bodyPath), tier.DiameterUnits));
            }

            if (File.Exists(iconPath) && VisibleWidth(iconPath) != PieceArtRules.ICON_SIZE)
            {
                errors.Add($"{iconPath}: the icon must be {PieceArtRules.ICON_SIZE} px wide.");
            }
        }

        /// <summary>
        /// Reads every pixel of the given PNGs and applies <see cref="GetPaletteErrors"/>, then checks that the
        /// palette file is the one the code produces.
        /// </summary>
        /// <param name="tiers">The tiers, whose colours must appear.</param>
        /// <param name="paths">The PNG paths to read.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckPalette(List<TierDefinition> tiers, List<string> paths, List<string> errors)
        {
            var opaque = new HashSet<Color32>();
            var partial = 0;
            foreach (var path in paths)
            {
                var texture = LoadPng(path);
                foreach (var pixel in texture.GetPixels32())
                {
                    if (pixel.a == 255)
                    {
                        opaque.Add(pixel);
                    }
                    else if (pixel.a != 0)
                    {
                        partial++;
                    }
                }

                UnityEngine.Object.DestroyImmediate(texture);
            }

            errors.AddRange(GetPaletteErrors(opaque, partial, tiers.Select(tier => CoikaPalette.Base(tier.Index))));

            if (!File.Exists(CoikaPalette.GPL_PATH) || File.ReadAllText(CoikaPalette.GPL_PATH) != CoikaPalette.ToGpl())
            {
                errors.Add($"{CoikaPalette.GPL_PATH} is missing or differs from the palette in code.");
            }
        }

        /// <summary>
        /// Checks the atlas and that every sprite, icon and the atlas is in the Theme-Cosmic group.
        /// </summary>
        /// <param name="paths">The paths of the bodies and icons.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckAtlasAndGroups(List<string> paths, List<string> errors)
        {
            if (AssetImporter.GetAtPath(PieceArtRules.ATLAS_PATH) is SpriteAtlasImporter atlas)
            {
                errors.AddRange(PieceAtlasSetup.GetErrors(atlas));
            }
            else
            {
                errors.Add($"{PieceArtRules.ATLAS_PATH}: missing.");
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("Addressables settings not found.");
                return;
            }

            foreach (var path in paths.Append(PieceArtRules.ATLAS_PATH))
            {
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                errors.AddRange(GetGroupErrors(path, entry?.parentGroup.Name));
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
