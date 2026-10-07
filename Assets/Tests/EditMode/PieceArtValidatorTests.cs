using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Coika.Data;
using Coika.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests of the checks of <see cref="PieceArtValidator"/> and <see cref="PieceAtlasSetup"/> with bad data, so each
    /// check is proven to fail, plus the rerun of the tier data setup.
    /// </summary>
    public class PieceArtValidatorTests
    {
        /// <summary>
        /// A body as wide as its collider has no collider error.
        /// </summary>
        [Test]
        public void GetColliderErrors_WidthMatchesDiameter_IsEmpty()
        {
            Assert.IsEmpty(PieceArtValidator.GetColliderErrors("body.png", 21, 1.3125f));
        }

        /// <summary>
        /// A body one pixel narrower than its collider (0.5 px of radius) is still within the tolerance, two are not.
        /// </summary>
        [Test]
        public void GetColliderErrors_WidthOffByOneAndTwoPixels_FailsOnlyForTwo()
        {
            Assert.IsEmpty(PieceArtValidator.GetColliderErrors("body.png", 20, 1.3125f));
            Assert.IsNotEmpty(PieceArtValidator.GetColliderErrors("body.png", 19, 1.3125f));
        }

        /// <summary>
        /// A texture in a piece art folder that belongs to no tier is reported.
        /// </summary>
        [Test]
        public void GetStrayErrors_ExtraFile_IsReported()
        {
            var errors = PieceArtValidator.GetStrayErrors(new[] { "a.png", "Tier_00_Dust.png" }, new[] { "a.png" });

            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("Tier_00_Dust.png", errors[0]);
        }

        /// <summary>
        /// The colours of the palette, with every tier colour present, give no palette error.
        /// </summary>
        [Test]
        public void GetPaletteErrors_WholePalette_IsEmpty()
        {
            Assert.IsEmpty(PieceArtValidator.GetPaletteErrors(CoikaPalette.Colors().ToList(), 0, TierColors()));
        }

        /// <summary>
        /// More than 32 distinct colours fail, even when the extra ones are far from the palette.
        /// </summary>
        [Test]
        public void GetPaletteErrors_MoreThanThirtyTwoColours_Fails()
        {
            var colours = CoikaPalette.Colors().ToList();
            colours.AddRange(Enumerable.Range(1, 3).Select(i => new Color32((byte)i, 0, 0, 255)));

            StringAssert.Contains("maximum", string.Join("\n", PieceArtValidator.GetPaletteErrors(colours, 0, TierColors())));
        }

        /// <summary>
        /// A colour that is not in the palette fails.
        /// </summary>
        [Test]
        public void GetPaletteErrors_ColourOutsideThePalette_Fails()
        {
            var colours = CoikaPalette.Colors().Append(new Color32(1, 2, 3, 255)).ToList();

            StringAssert.Contains("#010203", string.Join("\n", PieceArtValidator.GetPaletteErrors(colours, 0, TierColors())));
        }

        /// <summary>
        /// A tier colour that appears in no sprite fails.
        /// </summary>
        [Test]
        public void GetPaletteErrors_TierColourMissing_Fails()
        {
            var colours = CoikaPalette.Colors().Where(colour => !colour.Equals(CoikaPalette.Base(3))).ToList();

            StringAssert.Contains(CoikaPalette.ToHex(CoikaPalette.Base(3)), string.Join("\n", PieceArtValidator.GetPaletteErrors(colours, 0, TierColors())));
        }

        /// <summary>
        /// A semi-transparent pixel (anti-aliasing) fails.
        /// </summary>
        [Test]
        public void GetPaletteErrors_SemiTransparentPixels_Fails()
        {
            StringAssert.Contains("semi-transparent", string.Join("\n", PieceArtValidator.GetPaletteErrors(CoikaPalette.Colors().ToList(), 2, TierColors())));
        }

        /// <summary>
        /// Only the Theme-Cosmic group passes; another group or no entry fails.
        /// </summary>
        [Test]
        public void GetGroupErrors_OtherGroupOrNoEntry_Fails()
        {
            Assert.IsEmpty(PieceArtValidator.GetGroupErrors("a.png", PieceArtRules.GROUP_NAME));
            Assert.IsNotEmpty(PieceArtValidator.GetGroupErrors("a.png", "Core-Data"));
            Assert.IsNotEmpty(PieceArtValidator.GetGroupErrors("a.png", null));
        }

        /// <summary>
        /// The required atlas settings give no error.
        /// </summary>
        [Test]
        public void GetSettingsErrors_RequiredSettings_IsEmpty()
        {
            Assert.IsEmpty(PieceAtlasSetup.GetSettingsErrors(GoodPacking(), GoodTexture(), GoodPlatforms()));
        }

        /// <summary>
        /// Padding below 4, Tight Packing on, rotation on, a bilinear filter or a missing mobile override each fail.
        /// </summary>
        [Test]
        public void GetSettingsErrors_OneBadSetting_FailsNamingIt()
        {
            var packing = GoodPacking();
            packing.padding = 2;
            StringAssert.Contains("Padding", Join(PieceAtlasSetup.GetSettingsErrors(packing, GoodTexture(), GoodPlatforms())));

            packing = GoodPacking();
            packing.enableTightPacking = true;
            StringAssert.Contains("Tight Packing", Join(PieceAtlasSetup.GetSettingsErrors(packing, GoodTexture(), GoodPlatforms())));

            packing = GoodPacking();
            packing.enableRotation = true;
            StringAssert.Contains("Rotation", Join(PieceAtlasSetup.GetSettingsErrors(packing, GoodTexture(), GoodPlatforms())));

            var texture = GoodTexture();
            texture.filterMode = FilterMode.Bilinear;
            StringAssert.Contains("Filter", Join(PieceAtlasSetup.GetSettingsErrors(GoodPacking(), texture, GoodPlatforms())));

            var platforms = GoodPlatforms();
            platforms[1].overridden = false;
            StringAssert.Contains("iOS", Join(PieceAtlasSetup.GetSettingsErrors(GoodPacking(), GoodTexture(), platforms)));

            platforms = GoodPlatforms();
            platforms[0].format = TextureImporterFormat.ASTC_6x6;
            StringAssert.Contains("Android", Join(PieceAtlasSetup.GetSettingsErrors(GoodPacking(), GoodTexture(), platforms)));
        }

        /// <summary>
        /// An atlas that does not pack the bodies folder fails.
        /// </summary>
        [Test]
        public void GetPackableErrors_BodiesFolderNotPacked_Fails()
        {
            Assert.IsEmpty(PieceAtlasSetup.GetPackableErrors(new[] { PieceArtRules.BODIES_FOLDER }));
            Assert.IsNotEmpty(PieceAtlasSetup.GetPackableErrors(new string[0]));
            Assert.IsNotEmpty(PieceAtlasSetup.GetPackableErrors(new[] { PieceArtRules.ICONS_FOLDER }));
        }

        /// <summary>
        /// Running the tier data setup again leaves the final sprites byte-identical and the art valid (the placeholder
        /// generator must never overwrite final art).
        /// </summary>
        [Test]
        public void TierDataSetup_Rerun_LeavesTheFinalSpritesUnchanged()
        {
            var before = Fingerprint();

            TierDataSetup.Run();

            CollectionAssert.AreEqual(before, Fingerprint());
            var errors = PieceArtValidator.GetErrors();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        /// <summary>
        /// Lists the tier colours that must appear in the art.
        /// </summary>
        /// <returns>The base colour of every tier.</returns>
        private static List<Color32> TierColors()
        {
            return Enumerable.Range(0, ThemeDefinition.TIER_COUNT).Select(CoikaPalette.Base).ToList();
        }

        /// <summary>
        /// Joins error messages into one string.
        /// </summary>
        /// <param name="errors">The messages.</param>
        /// <returns>The messages on separate lines.</returns>
        private static string Join(IEnumerable<string> errors)
        {
            return string.Join("\n", errors);
        }

        /// <summary>
        /// Builds packing settings that satisfy the rules.
        /// </summary>
        /// <returns>Padding 4, Tight Packing and rotation off.</returns>
        private static SpriteAtlasPackingSettings GoodPacking()
        {
            return new SpriteAtlasPackingSettings { padding = PieceAtlasSetup.MIN_PADDING, enableTightPacking = false, enableRotation = false };
        }

        /// <summary>
        /// Builds texture settings that satisfy the rules.
        /// </summary>
        /// <returns>Point filter, no mip maps.</returns>
        private static SpriteAtlasTextureSettings GoodTexture()
        {
            return new SpriteAtlasTextureSettings { filterMode = FilterMode.Point, generateMipMaps = false };
        }

        /// <summary>
        /// Builds the Android and iOS settings that satisfy the rules.
        /// </summary>
        /// <returns>One overridden RGBA32 setting per platform of <see cref="SpritePlatformOverrides.Platforms"/>.</returns>
        private static List<TextureImporterPlatformSettings> GoodPlatforms()
        {
            return SpritePlatformOverrides.Platforms
                .Select(name => new TextureImporterPlatformSettings { name = name, overridden = true, format = TextureImporterFormat.RGBA32 })
                .ToList();
        }

        /// <summary>
        /// Reads the bytes and GUID of every body sprite and icon, to compare before and after a setup run.
        /// </summary>
        /// <returns>One string per file: its path, GUID and content hash.</returns>
        private static List<string> Fingerprint()
        {
            using (var md5 = MD5.Create())
            {
                return Directory.GetFiles(PieceArtRules.BODIES_FOLDER, "*.png").Concat(Directory.GetFiles(PieceArtRules.ICONS_FOLDER, "*.png"))
                    .OrderBy(path => path)
                    .Select(path => $"{path} {AssetDatabase.AssetPathToGUID(path)} {BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path)))}")
                    .ToList();
            }
        }
    }
}
