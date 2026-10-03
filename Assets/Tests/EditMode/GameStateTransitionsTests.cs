using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the table of legal state changes (issue #11).
    /// </summary>
    public class GameStateTransitionsTests
    {
        /// <summary>
        /// A run can start from every state, which makes starting while playing a restart.
        /// </summary>
        [TestCase(GameState.Boot)]
        [TestCase(GameState.Menu)]
        [TestCase(GameState.Playing)]
        [TestCase(GameState.Paused)]
        [TestCase(GameState.GameOver)]
        public void IsAllowed_ToPlaying_IsAllowedFromEveryState(GameState from)
        {
            Assert.IsTrue(GameStateTransitions.IsAllowed(from, GameState.Playing));
        }

        /// <summary>
        /// A game over can only follow a run in progress, playing or paused.
        /// </summary>
        [TestCase(GameState.Playing, true)]
        [TestCase(GameState.Paused, true)]
        [TestCase(GameState.Boot, false)]
        [TestCase(GameState.Menu, false)]
        [TestCase(GameState.GameOver, false)]
        public void IsAllowed_ToGameOver_RequiresARunInProgress(GameState from, bool expected)
        {
            Assert.AreEqual(expected, GameStateTransitions.IsAllowed(from, GameState.GameOver));
        }

        /// <summary>
        /// Only a run in progress can be paused.
        /// </summary>
        [TestCase(GameState.Playing, true)]
        [TestCase(GameState.Paused, false)]
        [TestCase(GameState.Menu, false)]
        [TestCase(GameState.GameOver, false)]
        [TestCase(GameState.Boot, false)]
        public void IsAllowed_ToPaused_RequiresPlaying(GameState from, bool expected)
        {
            Assert.AreEqual(expected, GameStateTransitions.IsAllowed(from, GameState.Paused));
        }

        /// <summary>
        /// The menu is reachable from the boot and from the end of a run, but not from the middle of one.
        /// </summary>
        [TestCase(GameState.Boot, true)]
        [TestCase(GameState.GameOver, true)]
        [TestCase(GameState.Paused, true)]
        [TestCase(GameState.Playing, false)]
        [TestCase(GameState.Menu, false)]
        public void IsAllowed_ToMenu_IsNotReachableWhilePlaying(GameState from, bool expected)
        {
            Assert.AreEqual(expected, GameStateTransitions.IsAllowed(from, GameState.Menu));
        }

        /// <summary>
        /// Nothing returns to the boot state.
        /// </summary>
        [TestCase(GameState.Menu)]
        [TestCase(GameState.Playing)]
        [TestCase(GameState.Paused)]
        [TestCase(GameState.GameOver)]
        [TestCase(GameState.Boot)]
        public void IsAllowed_ToBoot_IsNeverAllowed(GameState from)
        {
            Assert.IsFalse(GameStateTransitions.IsAllowed(from, GameState.Boot));
        }
    }
}
