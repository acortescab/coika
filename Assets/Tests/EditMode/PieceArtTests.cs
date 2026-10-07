using System;
using System.Collections;
using System.IO;
using System.Linq;
using Coika.Data;
using Coika.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests of the piece art: the drawing, the import rules and the validation of the shipped sprites (GDD §10).
    /// </summary>
    public class PieceArtTests
    {
        private const string TEMP_FOLDER = "Assets/Tests/EditMode/TempArt";
        private const string TEMP_SPRITE = TEMP_FOLDER + "/piece_99_test.png";

        /// <summary>
        /// One case per import rule: how to break it and what the error must mention.
        /// </summary>
        public static IEnumerable BrokenSettings
        {
            get
            {
                yield return Break("Texture Type", importer => importer.textureType = TextureImporterType.Default);
                yield return Break("Sprite Mode", importer => importer.spriteImportMode = SpriteImportMode.Multiple);
                yield return Break("Pixels Per Unit", importer => importer.spritePixelsPerUnit = 100f);
                yield return Break("Filter Mode", importer => importer.filterMode = FilterMode.Bilinear);
                yield return Break("Compression", importer => importer.textureCompression = TextureImporterCompression.Compressed);
                yield return Break("Mip Maps", importer => importer.mipmapEnabled = true);
                yield return Break("Pivot", importer =>
                {
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.TopLeft;
                    importer.SetTextureSettings(settings);
                });
                yield return Break("Android", importer => DisableOverride(importer, "Android"));
                yield return Break("iOS", importer => DisableOverride(importer, "iOS"));
            }
        }

        /// <summary>
        /// Removes the temporary texture the import tests create.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TEMP_FOLDER))
            {
                AssetDatabase.DeleteAsset(TEMP_FOLDER);
            }
        }

        /// <summary>
        /// The shipped art passes every check of the validator: names, import settings, palette, atlas, group and collider.
        /// </summary>
        [Test]
        public void PieceArtValidator_ShippedArt_HasNoErrors()
        {
            var errors = PieceArtValidator.GetErrors();

            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        /// <summary>
        /// A body is as wide as its tier diameter, so it needs no resizing at runtime.
        /// </summary>
        [Test]
        public void Render_EveryTier_FillsTheCanvasWidth()
        {
            foreach (var tier in PieceArtRules.FindTiers())
            {
                var size = BodySize(tier);

                Assert.AreEqual(size, VisibleWidth(PieceArtRenderer.Render(tier.Index, size, false), size), tier.name);
            }
        }

        /// <summary>
        /// Drawing uses nothing but palette colours and a closed outline: no opaque pixel touches a transparent one
        /// unless it is the outline.
        /// </summary>
        [Test]
        public void Render_EveryTier_UsesOnlyPaletteColoursAndAClosedOutline()
        {
            var palette = CoikaPalette.Colors().ToList();
            foreach (var tier in PieceArtRules.FindTiers())
            {
                var size = BodySize(tier);
                var pixels = PieceArtRenderer.Render(tier.Index, size, false);

                foreach (var pixel in pixels.Where(pixel => pixel.a != 0))
                {
                    Assert.AreEqual(255, pixel.a);
                    Assert.Contains(pixel, palette);
                }

                // The black hole shades with the outline colour, so its thickness cannot be told from the shade.
                AssertOutlineIsClosed(pixels, size, tier.Index != ThemeDefinition.TIER_COUNT - 1);
            }
        }

        /// <summary>
        /// A chart icon is 12 px wide, uses palette colours only and has no shade, so it is the simplified body.
        /// </summary>
        [Test]
        public void Render_IconOfEveryTier_Is12PxWithPaletteColoursAndNoShade()
        {
            var palette = CoikaPalette.Colors().ToList();
            foreach (var tier in PieceArtRules.FindTiers())
            {
                var pixels = PieceArtRenderer.Render(tier.Index, PieceArtRules.ICON_SIZE, true);

                Assert.AreEqual(PieceArtRules.ICON_SIZE, VisibleWidth(pixels, PieceArtRules.ICON_SIZE), tier.name);
                Assert.IsTrue(pixels.Where(pixel => pixel.a != 0).All(pixel => palette.Contains(pixel)), tier.name);
                if (tier.Index != ThemeDefinition.TIER_COUNT - 1)
                {
                    CollectionAssert.DoesNotContain(pixels, CoikaPalette.Shade(tier.Index), tier.name);
                }

                AssertOutlineIsClosed(pixels, PieceArtRules.ICON_SIZE, true);
            }
        }

        /// <summary>
        /// Drawing the same tier twice gives the same pixels.
        /// </summary>
        [Test]
        public void Render_SameArguments_GivesSamePixels()
        {
            CollectionAssert.AreEqual(PieceArtRenderer.Render(5, 42, false), PieceArtRenderer.Render(5, 42, false));
        }

        /// <summary>
        /// A texture that has not got the settings fails the import rules, and a texture with them passes.
        /// </summary>
        [Test]
        public void ImporterRules_DefaultTexture_FailsUntilApplied()
        {
            var importer = ImportTempTexture();

            Assert.IsNotEmpty(PieceArtRules.GetImporterErrors(importer));

            PieceArtRules.Apply(importer);

            Assert.IsEmpty(PieceArtRules.GetImporterErrors(importer));
        }

        /// <summary>
        /// Breaking one setting of a correct sprite fails the import rules with a message about that setting.
        /// </summary>
        /// <param name="breakIt">Breaks one setting of the importer.</param>
        /// <param name="expected">Text the error must contain.</param>
        [TestCaseSource(nameof(BrokenSettings))]
        public void ImporterRules_OneBrokenSetting_FailsNamingIt(Action<TextureImporter> breakIt, string expected)
        {
            var importer = ImportTempTexture();
            PieceArtRules.Apply(importer);

            breakIt(importer);

            StringAssert.Contains(expected, string.Join("\n", PieceArtRules.GetImporterErrors(importer)));
        }

        /// <summary>
        /// A sprite with only the Android override is not compliant: iOS needs it too.
        /// </summary>
        [Test]
        public void PlatformOverrides_OnlyAndroidOverridden_IsNotCompliant()
        {
            var importer = ImportTempTexture();
            PieceArtRules.Apply(importer);
            Assert.IsTrue(SpritePlatformOverrides.IsCompliant(importer));

            DisableOverride(importer, "iOS");

            Assert.IsFalse(SpritePlatformOverrides.IsCompliant(importer));
        }

        /// <summary>
        /// Only lower-case piece_XX_name.png and icon_XX_name.png files under Assets/Art are piece art.
        /// </summary>
        /// <param name="path">A project path.</param>
        /// <param name="expected">Whether it is piece art.</param>
        [TestCase("Assets/Art/Sprites/Tiers/piece_04_dwarf_planet.png", true)]
        [TestCase("Assets/Art/Sprites/Icons/icon_00_dust.png", true)]
        [TestCase("Assets/Art/Sprites/Tiers/Tier_04_DwarfPlanet.png", false)]
        [TestCase("Assets/Art/Sprites/Tiers/piece_4_dust.png", false)]
        [TestCase("Assets/Art/Sprites/Tiers/Piece_04_dust.png", false)]
        [TestCase("Assets/Art/Sprites/Tiers/piece_04_dust.jpg", false)]
        [TestCase("Assets/Tests/EditMode/TempArt/piece_99_test.png", false)]
        public void IsPieceArt_Path_MatchesOnlyPieceArt(string path, bool expected)
        {
            Assert.AreEqual(expected, PieceArtRules.IsPieceArt(path));
        }

        /// <summary>
        /// Tier asset names become lower-case snake-case sprite and icon names, for all 11 tiers.
        /// </summary>
        [Test]
        public void BodyName_EveryTier_IsSnakeCase()
        {
            var expected = new[]
            {
                "dust", "pebble", "asteroid", "moon", "dwarf_planet", "rocky_planet",
                "ocean_planet", "gas_giant", "star", "neutron_star", "black_hole",
            };
            var tiers = PieceArtRules.FindTiers();

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual($"piece_{i:00}_{expected[i]}", PieceArtRules.BodyName(tiers[i]));
                Assert.AreEqual($"icon_{i:00}_{expected[i]}", PieceArtRules.IconName(tiers[i]));
            }
        }

        /// <summary>
        /// Builds one case of <see cref="BrokenSettings"/>.
        /// </summary>
        /// <param name="expected">Text the error must contain.</param>
        /// <param name="breakIt">Breaks one setting of the importer.</param>
        /// <returns>The test case.</returns>
        private static TestCaseData Break(string expected, Action<TextureImporter> breakIt)
        {
            return new TestCaseData(breakIt, expected).SetName($"ImporterRules_Broken{expected.Replace(" ", string.Empty)}_Fails");
        }

        /// <summary>
        /// Turns off the override of one platform, in memory only.
        /// </summary>
        /// <param name="importer">The importer to modify.</param>
        /// <param name="platform">The platform name.</param>
        private static void DisableOverride(TextureImporter importer, string platform)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = false;
            importer.SetPlatformTextureSettings(settings);
        }

        /// <summary>
        /// Gives the side in pixels of a tier body: its diameter at 16 pixels per unit.
        /// </summary>
        /// <param name="tier">The tier.</param>
        /// <returns>The body width in pixels.</returns>
        private static int BodySize(TierDefinition tier)
        {
            return Mathf.RoundToInt(tier.DiameterUnits * TierDefinition.PixelsPerUnit);
        }

        /// <summary>
        /// Creates a small PNG outside the piece art folders (so the postprocessor leaves its default settings;
        /// <see cref="PieceArtRules.IsPieceArt"/> needs a path under Assets/Art) and returns its importer. The test
        /// settings are never saved.
        /// </summary>
        /// <returns>The importer of the temporary texture.</returns>
        private static TextureImporter ImportTempTexture()
        {
            if (!AssetDatabase.IsValidFolder(TEMP_FOLDER))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(TEMP_FOLDER).Replace('\\', '/'), Path.GetFileName(TEMP_FOLDER));
            }

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            File.WriteAllBytes(TEMP_SPRITE, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TEMP_SPRITE, ImportAssetOptions.ForceSynchronousImport);
            return (TextureImporter)AssetImporter.GetAtPath(TEMP_SPRITE);
        }

        /// <summary>
        /// Measures the horizontal extent of the visible pixels of a square image.
        /// </summary>
        /// <param name="pixels">The pixels.</param>
        /// <param name="size">Side of the image.</param>
        /// <returns>The width from the leftmost to the rightmost pixel with alpha above zero.</returns>
        private static int VisibleWidth(Color32[] pixels, int size)
        {
            var columns = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).Select(i => i % size).ToList();
            return columns.Max() - columns.Min() + 1;
        }

        /// <summary>
        /// Asserts that every opaque pixel next to a transparent one, or the image edge, is the outline colour, and
        /// that the outline is 1 px thick: no outline pixel has all four neighbours opaque and outline-coloured.
        /// </summary>
        /// <param name="pixels">The pixels.</param>
        /// <param name="size">Side of the image.</param>
        /// <param name="checkThickness">False when another colour of the image equals the outline colour.</param>
        private static void AssertOutlineIsClosed(Color32[] pixels, int size, bool checkThickness)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y * size + x].a == 0)
                    {
                        continue;
                    }

                    var onEdge = x == 0 || y == 0 || x == size - 1 || y == size - 1
                        || pixels[y * size + x - 1].a == 0 || pixels[y * size + x + 1].a == 0
                        || pixels[(y - 1) * size + x].a == 0 || pixels[(y + 1) * size + x].a == 0;
                    if (onEdge)
                    {
                        Assert.AreEqual(CoikaPalette.Outline, pixels[y * size + x], $"({x},{y}) of {size}");
                    }
                    else if (checkThickness)
                    {
                        var allNeighboursOutline = pixels[y * size + x - 1].Equals(CoikaPalette.Outline)
                            && pixels[y * size + x + 1].Equals(CoikaPalette.Outline)
                            && pixels[(y - 1) * size + x].Equals(CoikaPalette.Outline)
                            && pixels[(y + 1) * size + x].Equals(CoikaPalette.Outline);
                        Assert.IsFalse(allNeighboursOutline && pixels[y * size + x].Equals(CoikaPalette.Outline), $"thick outline at ({x},{y}) of {size}");
                    }
                }
            }
        }
    }
}
