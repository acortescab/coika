using System.Collections.Generic;
using NUnit.Framework;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// The score of a whole scripted run equals the value computed by hand from the GDD formulas (§3.2, §4). It uses
    /// the asset service double with the real merge scores of the GDD table.
    /// </summary>
    public class ScoreIntegrationPlayModeTests : HarnessTestBase
    {
        private const float DROP_SPACING_SECONDS = 3f;
        private const float SETTLE_SECONDS = 3f;

        /// <summary>
        /// Four pieces of tiers 0, 0, 1 and 2 dropped at the same X, one every 3 s, merge into one tier 3 piece.
        /// <para>
        /// Hand calculation (GDD §3.2 merge scores: tier 1 = 3, tier 2 = 6, tier 3 = 10; §4 drop points = tier index):
        /// </para>
        /// <list type="bullet">
        /// <item><description>Drops: 0 + 0 + 1 + 2 = 3 points, with no multiplier.</description></item>
        /// <item><description>Dropping the second tier 0 on the first merges them into tier 1: merge score 3.</description></item>
        /// <item><description>Dropping tier 1 on that piece merges them into tier 2: merge score 6.</description></item>
        /// <item><description>Dropping tier 2 on that piece merges them into tier 3: merge score 10.</description></item>
        /// <item><description>The merges are about 3 s apart, more than the 1.0 s combo window, so each one starts a new
        /// combo with multiplier x1.00 and floor(score x 1.00) is the merge score itself: 3 + 6 + 10 = 19.</description></item>
        /// <item><description>Total: 3 + 19 = 22, three merges, highest tier 3, four pieces dropped, and a single tier 3
        /// piece left on the board.</description></item>
        /// </list>
        /// </summary>
        [Test]
        public void Run_WithKnownMerges_ScoresTheValueComputedByHand()
        {
            const int EXPECTED_SCORE = 22;
            var options = new SimulationOptions
            {
                Seed = 1,
                ForcedOpening = new[] { 0, 0, 1, 2 },
                MinSecondsBetweenDrops = DROP_SPACING_SECONDS,
                SettleSeconds = SETTLE_SECONDS,
            };

            var result = SimulationRunner.Run(options, DropScripts.Column(0f, 4));

            Assert.AreEqual(EXPECTED_SCORE, result.Score, result.ToString());
            Assert.AreEqual(3, result.Merges);
            Assert.AreEqual(3, result.HighestTier);
            Assert.AreEqual(4, result.PiecesDropped);
            Assert.IsFalse(result.GameOver);

            var tiers = new List<int>();
            foreach (var piece in result.Pieces)
            {
                tiers.Add(piece.Tier);
            }

            CollectionAssert.AreEqual(new[] { 3 }, tiers, "Only the merged tier 3 piece should be left.");
        }
    }
}
