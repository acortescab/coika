using System;
using System.Linq;
using Coika.Data;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the validation and the immutability of <see cref="SpawnSettings"/> (issue #5): an invalid config gives
    /// a clear message and never loops or hangs.
    /// </summary>
    public class SpawnSettingsTests
    {
        private static readonly int[] DefaultOpening = { 0, 1, 0 };

        /// <summary>
        /// The GDD defaults are valid.
        /// </summary>
        [Test]
        public void Validate_WithDefaultValues_ReturnsNoErrors()
        {
            var errors = SpawnSettings.Validate(TestSpawnSettings.DefaultWeights, 5, 3, DefaultOpening);

            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        /// <summary>
        /// Each invalid value is reported, with a message that names what is wrong.
        /// </summary>
        [TestCase(new[] { 30f, 28f, 20f }, 5, 3, new[] { 0, 1, 0 }, "3 spawn weights", TestName = "Validate_WeightsLengthDiffersFromTierCount_ReportsIt")]
        [TestCase(new[] { 30f, 28f, 20f, 14f, 8f }, 0, 3, new[] { 0, 1, 0 }, "between 1 and 11", TestName = "Validate_TierCountZero_ReportsIt")]
        [TestCase(new[] { 30f, 28f, 20f, 14f, 8f }, 12, 3, new[] { 0, 1, 0 }, "between 1 and 11", TestName = "Validate_TierCountTwelve_ReportsIt")]
        [TestCase(new[] { 30f, 28f, 20f, 14f, 8f }, 5, 0, new[] { 0, 1, 0 }, "at least 1", TestName = "Validate_AntiStreakMaxZero_ReportsIt")]
        [TestCase(new[] { 30f, 28f, 20f, 14f, 8f }, 5, 3, new[] { 0, 5, 0 }, "piece 1 is tier 5", TestName = "Validate_ForcedTierOutOfRange_ReportsTheIndex")]
        [TestCase(new[] { 30f, -1f, 20f, 14f, 8f }, 5, 3, new[] { 0, 1, 0 }, "weight of tier 1", TestName = "Validate_NegativeWeight_ReportsTheTier")]
        [TestCase(new[] { 30f, float.NaN, 20f, 14f, 8f }, 5, 3, new[] { 0, 1, 0 }, "weight of tier 1", TestName = "Validate_NotANumberWeight_ReportsTheTier")]
        [TestCase(new[] { 0f, 0f, 0f, 0f, 0f }, 5, 3, new[] { 0, 1, 0 }, "above zero", TestName = "Validate_AllWeightsZero_ReportsIt")]
        [TestCase(new[] { 30f, 28f, 20f, 14f, 8f }, 5, 3, new[] { 2, 2, 2, 2 }, "more than 3 times in a row", TestName = "Validate_ForcedOpeningBreakingTheStreak_ReportsIt")]
        public void Validate_WithAnInvalidValue_ReportsAClearMessage(float[] weights, int tierCount, int antiStreakMax, int[] opening, string expectedFragment)
        {
            var errors = SpawnSettings.Validate(weights, tierCount, antiStreakMax, opening);

            Assert.IsTrue(errors.Any(error => error.Contains(expectedFragment)), $"Expected a message containing '{expectedFragment}', got: {string.Join(" | ", errors)}");
        }

        /// <summary>
        /// Missing arrays are reported instead of throwing.
        /// </summary>
        [Test]
        public void Validate_WithNullArrays_ReportsThemWithoutThrowing()
        {
            var errors = SpawnSettings.Validate(null, 5, 3, null);

            Assert.AreEqual(2, errors.Count, string.Join(" | ", errors));
        }

        /// <summary>
        /// A forced opening that repeats a tier exactly as many times as the maximum is allowed.
        /// </summary>
        [Test]
        public void Validate_ForcedOpeningAtTheStreakLimit_IsValid()
        {
            var errors = SpawnSettings.Validate(TestSpawnSettings.DefaultWeights, 5, 3, new[] { 2, 2, 2 });

            Assert.IsEmpty(errors);
        }

        /// <summary>
        /// An empty forced opening means none and is valid.
        /// </summary>
        [Test]
        public void Validate_WithAnEmptyForcedOpening_IsValid()
        {
            Assert.IsEmpty(SpawnSettings.Validate(TestSpawnSettings.DefaultWeights, 5, 3, new int[0]));
        }

        /// <summary>
        /// Building invalid settings throws one exception whose message lists every problem.
        /// </summary>
        [Test]
        public void Constructor_WithInvalidValues_ThrowsAnArgumentExceptionListingEveryProblem()
        {
            var exception = Assert.Throws<ArgumentException>(() => new SpawnSettings(new[] { 0f, 0f }, 0, 0, new[] { 9 }));

            StringAssert.Contains("between 1 and 11", exception.Message);
            StringAssert.Contains("at least 1", exception.Message);
            StringAssert.Contains("above zero", exception.Message);
        }

        /// <summary>
        /// The settings copy the arrays, so changing them afterwards changes nothing.
        /// </summary>
        [Test]
        public void Constructor_WhenTheCallerChangesTheArraysAfterwards_LeavesTheSettingsUnchanged()
        {
            var weights = new[] { 30f, 28f, 20f, 14f, 8f };
            var opening = new[] { 0, 1, 0 };
            var settings = new SpawnSettings(weights, 5, 3, opening);

            weights[0] = 999f;
            opening[0] = 4;

            Assert.AreEqual(30f, settings.GetWeight(0));
            Assert.AreEqual(0, settings.GetForcedTier(0));
            Assert.AreEqual(5, settings.TierCount);
            Assert.AreEqual(3, settings.AntiStreakMax);
            Assert.AreEqual(3, settings.ForcedOpeningLength);
        }
    }
}
