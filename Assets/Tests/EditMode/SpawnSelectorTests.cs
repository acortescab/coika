using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the weighted choice of <see cref="SpawnSelector"/> with exact rolls, so the maths is verified without
    /// statistics (issue #5).
    /// </summary>
    public class SpawnSelectorTests
    {
        /// <summary>
        /// Builds settings with the given weights and no forced opening.
        /// </summary>
        /// <param name="weights">Relative weight of each tier.</param>
        private static SpawnSettings Settings(params float[] weights)
        {
            return new SpawnSettings(weights, weights.Length, 3, new int[0]);
        }

        /// <summary>
        /// A roll of zero lands on the first tier that has weight.
        /// </summary>
        [Test]
        public void Pick_WithRollZero_ReturnsTheFirstTierWithWeight()
        {
            Assert.AreEqual(1, SpawnSelector.Pick(Settings(0f, 5f, 5f), SpawnSelector.NO_TIER, 0.0));
        }

        /// <summary>
        /// A roll just below one lands on the last tier that has weight.
        /// </summary>
        [Test]
        public void Pick_WithRollJustBelowOne_ReturnsTheLastTierWithWeight()
        {
            Assert.AreEqual(1, SpawnSelector.Pick(Settings(5f, 5f, 0f), SpawnSelector.NO_TIER, 0.9999999));
        }

        /// <summary>
        /// The rolls split the range in proportion to the weights: weights 1, 1 and 2 split it at 0.25 and 0.5.
        /// </summary>
        [TestCase(0.10, 0)]
        [TestCase(0.24, 0)]
        [TestCase(0.26, 1)]
        [TestCase(0.49, 1)]
        [TestCase(0.51, 2)]
        [TestCase(0.99, 2)]
        public void Pick_WithRolls_SplitsTheRangeByTheWeights(double roll, int expectedTier)
        {
            Assert.AreEqual(expectedTier, SpawnSelector.Pick(Settings(1f, 1f, 2f), SpawnSelector.NO_TIER, roll));
        }

        /// <summary>
        /// The excluded tier is never returned, whatever the roll.
        /// </summary>
        [Test]
        public void Pick_WithAnExcludedTier_NeverReturnsIt()
        {
            var settings = Settings(1f, 1f, 2f);

            for (double roll = 0.0; roll < 1.0; roll += 0.01)
            {
                Assert.AreNotEqual(2, SpawnSelector.Pick(settings, 2, roll), $"roll {roll}");
            }
        }

        /// <summary>
        /// Leaving a tier out renormalizes the rest: with weights 1, 1 and 2 and the last tier excluded, the two
        /// remaining tiers split the range in half.
        /// </summary>
        [Test]
        public void Pick_WithAnExcludedTier_RenormalizesTheOthers()
        {
            var settings = Settings(1f, 1f, 2f);

            Assert.AreEqual(0, SpawnSelector.Pick(settings, 2, 0.49));
            Assert.AreEqual(1, SpawnSelector.Pick(settings, 2, 0.51));
        }

        /// <summary>
        /// A tier with no weight is never returned.
        /// </summary>
        [Test]
        public void Pick_WithAZeroWeightTier_NeverReturnsIt()
        {
            var settings = Settings(3f, 0f, 3f);

            for (double roll = 0.0; roll < 1.0; roll += 0.01)
            {
                Assert.AreNotEqual(1, SpawnSelector.Pick(settings, SpawnSelector.NO_TIER, roll), $"roll {roll}");
            }
        }

        /// <summary>
        /// With a single spawnable tier that is excluded, the exclusion is ignored: the tier repeats and nothing hangs.
        /// </summary>
        [Test]
        public void Pick_WithASingleTierThatIsExcluded_IgnoresTheExclusion()
        {
            Assert.AreEqual(0, SpawnSelector.Pick(Settings(5f), 0, 0.5));
        }

        /// <summary>
        /// When only the excluded tier has weight, the exclusion is ignored too.
        /// </summary>
        [Test]
        public void Pick_WhenOnlyTheExcludedTierHasWeight_IgnoresTheExclusion()
        {
            Assert.AreEqual(0, SpawnSelector.Pick(Settings(5f, 0f), 0, 0.7));
        }
    }
}
