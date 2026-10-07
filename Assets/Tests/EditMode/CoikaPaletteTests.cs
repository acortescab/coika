using System.IO;
using System.Linq;
using Coika.Data;
using Coika.Tools;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests of the shared palette of the piece art (GDD §10).
    /// </summary>
    public class CoikaPaletteTests
    {
        /// <summary>
        /// The palette stays within the 32-colour budget.
        /// </summary>
        [Test]
        public void Colors_Always_AreAtMostThirtyTwoDistinct()
        {
            var colors = CoikaPalette.Colors();

            Assert.LessOrEqual(colors.Count, CoikaPalette.MAX_COLORS);
            Assert.AreEqual(colors.Count, colors.Distinct().Count());
        }

        /// <summary>
        /// Every tier colour of the shipped TierDefinition assets is a palette colour, so particles and sprites match.
        /// </summary>
        [Test]
        public void Colors_Always_IncludeTheElevenTierColours()
        {
            var tiers = PieceArtRules.FindTiers();

            Assert.AreEqual(ThemeDefinition.TIER_COUNT, tiers.Count);
            foreach (var tier in tiers)
            {
                Assert.AreEqual(CoikaPalette.Base(tier.Index), (Color32)tier.TierColor, tier.name);
                Assert.Contains(CoikaPalette.Base(tier.Index), CoikaPalette.Colors().ToList(), tier.name);
            }
        }

        /// <summary>
        /// The committed palette file is the one the code produces.
        /// </summary>
        [Test]
        public void GplFile_Always_ListsThePaletteInCode()
        {
            Assert.IsTrue(File.Exists(CoikaPalette.GPL_PATH), CoikaPalette.GPL_PATH);
            Assert.AreEqual(CoikaPalette.ToGpl(), File.ReadAllText(CoikaPalette.GPL_PATH));
        }
    }
}
