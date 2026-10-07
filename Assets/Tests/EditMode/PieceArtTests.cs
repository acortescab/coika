using System.IO;
using System.Linq;
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
        private const string TempFolder = "Assets/Tests/EditMode/TempArt";
        private const string TempSprite = TempFolder + "/piece_99_test.png";

        /// <summary>
        /// Removes the temporary texture the import tests create.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
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
                var size = Mathf.RoundToInt(tier.DiameterUnits * 16f);
                var pixels = PieceArtRenderer.Render(tier.Index, size, false);

                Assert.AreEqual(size, VisibleWidth(pixels, size), tier.name);
            }
        }

        /// <summary>
        /// Drawing uses nothing but palette colours and a closed outline: no opaque pixel touches a transparent one
        /// unless it is the outline.
        /// </summary>
        [Test]
        public void Render_EveryTier_UsesOnlyPaletteColoursAndAClosedOutline()
        {
            var palette = CoikaPalette.Colors();
            foreach (var tierIndex in Enumerable.Range(0, 11))
            {
                var size = Mathf.RoundToInt(PieceArtRules.FindTiers()[tierIndex].DiameterUnits * 16f);
                var pixels = PieceArtRenderer.Render(tierIndex, size, false);

                foreach (var pixel in pixels.Where(pixel => pixel.a != 0))
                {
                    Assert.AreEqual(255, pixel.a);
                    Assert.Contains(pixel, palette.ToList());
                }

                AssertOutlineIsClosed(pixels, size);
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
        /// A sprite switched to bilinear filtering fails the import rules.
        /// </summary>
        [Test]
        public void ImporterRules_BilinearFilter_Fails()
        {
            var importer = ImportTempTexture();
            PieceArtRules.Apply(importer);

            importer.filterMode = FilterMode.Bilinear;

            StringAssert.Contains("Filter Mode", string.Join("\n", PieceArtRules.GetImporterErrors(importer)));
        }

        /// <summary>
        /// A sprite switched to a compressed format fails the import rules.
        /// </summary>
        [Test]
        public void ImporterRules_CompressedTexture_Fails()
        {
            var importer = ImportTempTexture();
            PieceArtRules.Apply(importer);

            importer.textureCompression = TextureImporterCompression.Compressed;

            StringAssert.Contains("Compression", string.Join("\n", PieceArtRules.GetImporterErrors(importer)));
        }

        /// <summary>
        /// A sprite without the Android or iOS override fails the import rules.
        /// </summary>
        [Test]
        public void ImporterRules_MissingIosOverride_Fails()
        {
            var importer = ImportTempTexture();
            PieceArtRules.Apply(importer);

            var ios = importer.GetPlatformTextureSettings("iOS");
            ios.overridden = false;
            importer.SetPlatformTextureSettings(ios);

            StringAssert.Contains("iOS", string.Join("\n", PieceArtRules.GetImporterErrors(importer)));
        }

        /// <summary>
        /// Tier asset names become lower-case snake-case sprite names.
        /// </summary>
        [Test]
        public void BodyName_DwarfPlanet_IsSnakeCase()
        {
            var tier = PieceArtRules.FindTiers()[4];

            Assert.AreEqual("piece_04_dwarf_planet", PieceArtRules.BodyName(tier));
            Assert.AreEqual("icon_04_dwarf_planet", PieceArtRules.IconName(tier));
        }

        /// <summary>
        /// Creates a small PNG outside the piece art folders (so the postprocessor leaves its default settings) and
        /// returns its importer. The test settings are never saved.
        /// </summary>
        /// <returns>The importer of the temporary texture.</returns>
        private static TextureImporter ImportTempTexture()
        {
            Directory.CreateDirectory(TempFolder);
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            File.WriteAllBytes(TempSprite, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TempSprite, ImportAssetOptions.ForceSynchronousImport);
            return (TextureImporter)AssetImporter.GetAtPath(TempSprite);
        }

        /// <summary>
        /// Measures the width of the opaque pixels of a square image.
        /// </summary>
        /// <param name="pixels">The pixels.</param>
        /// <param name="size">Side of the image.</param>
        /// <returns>The width of the bounding box of the pixels with alpha above zero.</returns>
        private static int VisibleWidth(Color32[] pixels, int size)
        {
            var columns = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a != 0).Select(i => i % size).ToList();
            return columns.Max() - columns.Min() + 1;
        }

        /// <summary>
        /// Asserts that every opaque pixel next to a transparent one, or the image edge, is the outline colour.
        /// </summary>
        /// <param name="pixels">The pixels.</param>
        /// <param name="size">Side of the image.</param>
        private static void AssertOutlineIsClosed(Color32[] pixels, int size)
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
                }
            }
        }
    }
}
