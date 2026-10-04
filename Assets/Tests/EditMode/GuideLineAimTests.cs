using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests the visibility rules of the guide line (issue #28), which are plain logic.
    /// </summary>
    public class GuideLineAimTests
    {
        /// <summary>
        /// The guide shows while the player is pressing on a held piece and the setting is on.
        /// </summary>
        [Test]
        public void ShouldShow_WhilePressingOnAHeldPiece_IsTrue()
        {
            Assert.IsTrue(GuideLineAim.ShouldShow(true, true, true));
        }

        /// <summary>
        /// The guide is hidden when the player is not pressing (after a drop, during the cooldown, paused, game over).
        /// </summary>
        [Test]
        public void ShouldShow_WhenNotPressing_IsFalse()
        {
            Assert.IsFalse(GuideLineAim.ShouldShow(false, true, true));
        }

        /// <summary>
        /// The guide is hidden when no piece is held.
        /// </summary>
        [Test]
        public void ShouldShow_WithoutAHeldPiece_IsFalse()
        {
            Assert.IsFalse(GuideLineAim.ShouldShow(true, false, true));
        }

        /// <summary>
        /// The guide is hidden when the player turned the setting off.
        /// </summary>
        [Test]
        public void ShouldShow_WithTheSettingOff_IsFalse()
        {
            Assert.IsFalse(GuideLineAim.ShouldShow(true, true, false));
        }
    }
}
