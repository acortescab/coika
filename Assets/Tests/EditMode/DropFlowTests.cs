using System;
using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the rules of <see cref="DropFlow"/> (issue #6) with exact time steps: the substates, the cooldown,
    /// which releases count, the clamp and the capped follow.
    /// </summary>
    public class DropFlowTests
    {
        private const float COOLDOWN = 0.5f;
        private const float STEP = 0.02f;
        private const float TOLERANCE = 0.0001f;

        private DropFlow _flow;

        /// <summary>
        /// Creates a flow with the GDD cooldown.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _flow = new DropFlow(COOLDOWN);
        }

        /// <summary>
        /// Presses and releases while aiming, which starts the cooldown.
        /// </summary>
        private void Drop()
        {
            _flow.Press();
            Assert.IsTrue(_flow.TryRelease(), "The release must drop.");
        }

        /// <summary>
        /// A new flow is aiming.
        /// </summary>
        [Test]
        public void Constructor_Always_StartsAiming()
        {
            Assert.AreEqual(DropState.Aiming, _flow.State);
        }

        /// <summary>
        /// A negative cooldown is a programming error.
        /// </summary>
        [Test]
        public void Constructor_WithANegativeCooldown_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DropFlow(-1f), "cooldown");
        }

        /// <summary>
        /// A press and its release drop the piece and start the cooldown.
        /// </summary>
        [Test]
        public void TryRelease_AfterAPressWhileAiming_DropsAndStartsTheCooldown()
        {
            Drop();

            Assert.AreEqual(DropState.Dropping, _flow.State);
        }

        /// <summary>
        /// The press is on from its start (while aiming) until its release, cancel or drop.
        /// </summary>
        [Test]
        public void IsPressing_FromThePressUntilItsReleaseOrCancel_IsTrue()
        {
            Assert.IsFalse(_flow.IsPressing, "Nothing is pressed at first.");

            _flow.Press();
            Assert.IsTrue(_flow.IsPressing, "A press while aiming counts.");
            _flow.CancelPress();
            Assert.IsFalse(_flow.IsPressing, "A cancel ends it.");

            _flow.Press();
            _flow.TryRelease();
            Assert.IsFalse(_flow.IsPressing, "The release ends it.");

            _flow.Press();
            Assert.IsFalse(_flow.IsPressing, "A press during the cooldown is ignored.");
        }

        /// <summary>
        /// A release without a press does nothing.
        /// </summary>
        [Test]
        public void TryRelease_WithoutAPress_DoesNothing()
        {
            Assert.IsFalse(_flow.TryRelease());
            Assert.AreEqual(DropState.Aiming, _flow.State);
        }

        /// <summary>
        /// The press is used up by the release: a second release needs a new press.
        /// </summary>
        [Test]
        public void TryRelease_Twice_DropsOnlyOnce()
        {
            Drop();
            _flow.Tick(COOLDOWN);

            Assert.IsFalse(_flow.TryRelease());
        }

        /// <summary>
        /// The cooldown lasts exactly 25 steps of 0.02 s: still dropping after 24, aiming after 25.
        /// </summary>
        [Test]
        public void Tick_AfterTheCooldown_ReturnsToAimingOnTheTwentyFifthStep()
        {
            Drop();

            for (int i = 1; i <= 24; i++)
            {
                Assert.IsFalse(_flow.Tick(STEP), $"step {i}");
                Assert.AreEqual(DropState.Dropping, _flow.State, $"step {i}");
            }

            Assert.IsTrue(_flow.Tick(STEP), "step 25 ends the cooldown");
            Assert.AreEqual(DropState.Aiming, _flow.State);
        }

        /// <summary>
        /// Ticking while aiming changes nothing.
        /// </summary>
        [Test]
        public void Tick_WhileAiming_ReturnsFalseAndChangesNothing()
        {
            Assert.IsFalse(_flow.Tick(1f));
            Assert.AreEqual(DropState.Aiming, _flow.State);
        }

        /// <summary>
        /// With no cooldown the state goes straight back to aiming.
        /// </summary>
        [Test]
        public void TryRelease_WithNoCooldown_StaysAiming()
        {
            var flow = new DropFlow(0f);
            flow.Press();

            Assert.IsTrue(flow.TryRelease());
            Assert.AreEqual(DropState.Aiming, flow.State);
        }

        /// <summary>
        /// A press that begins during the cooldown is ignored, even if it is released after the cooldown ends.
        /// </summary>
        [Test]
        public void TryRelease_AfterAPressDuringTheCooldown_DoesNotDrop()
        {
            Drop();
            _flow.Press();
            _flow.Tick(COOLDOWN);

            Assert.IsFalse(_flow.TryRelease());
        }

        /// <summary>
        /// A press and a release in the very step in which the cooldown ends do drop, because the cooldown is
        /// ended first.
        /// </summary>
        [Test]
        public void TryRelease_InTheStepTheCooldownEnds_Drops()
        {
            Drop();
            for (int i = 0; i < 25; i++)
            {
                _flow.Tick(STEP);
            }

            _flow.Press();

            Assert.IsTrue(_flow.TryRelease());
        }

        /// <summary>
        /// Begin goes back to aiming with no press pending.
        /// </summary>
        [Test]
        public void Begin_AfterADrop_ReturnsToAimingAndForgetsThePress()
        {
            Drop();
            _flow.Press();

            _flow.Begin();

            Assert.AreEqual(DropState.Aiming, _flow.State);
            Assert.IsFalse(_flow.TryRelease());
        }

        /// <summary>
        /// The clamp keeps the whole piece inside the interior for a small and a large piece, at both walls.
        /// </summary>
        [TestCase(-100f, 0.5f, -4.5f)]
        [TestCase(100f, 0.5f, 4.5f)]
        [TestCase(-100f, 2.5f, -2.5f)]
        [TestCase(100f, 2.5f, 2.5f)]
        [TestCase(1f, 2.5f, 1f)]
        public void ClampX_WithAPieceAtAWall_KeepsTheWholePieceInside(float x, float radius, float expected)
        {
            Assert.AreEqual(expected, DropFlow.ClampX(x, radius, -5f, 5f), TOLERANCE);
        }

        /// <summary>
        /// A piece wider than the interior is centred.
        /// </summary>
        [Test]
        public void ClampX_WithAPieceWiderThanTheInterior_CentersIt()
        {
            Assert.AreEqual(0f, DropFlow.ClampX(3f, 6f, -5f, 5f), TOLERANCE);
        }

        /// <summary>
        /// The follow never moves more than the maximum speed times the time, however far the target is.
        /// </summary>
        [Test]
        public void Follow_WithAFarTarget_MovesAtTheMaximumSpeed()
        {
            Assert.AreEqual(0.8f, DropFlow.Follow(0f, 1000f, 40f, STEP), TOLERANCE);
            Assert.AreEqual(-0.8f, DropFlow.Follow(0f, -1000f, 40f, STEP), TOLERANCE);
        }

        /// <summary>
        /// The follow stops on a target that is closer than one step.
        /// </summary>
        [Test]
        public void Follow_WithANearTarget_ArrivesWithoutOvershooting()
        {
            Assert.AreEqual(0.3f, DropFlow.Follow(0f, 0.3f, 40f, STEP), TOLERANCE);
        }

        /// <summary>
        /// A cancelled press is forgotten: its release drops nothing and the flow keeps aiming.
        /// </summary>
        [Test]
        public void CancelPress_AfterAPress_MakesTheReleaseDropNothing()
        {
            _flow.Press();

            _flow.CancelPress();

            Assert.IsFalse(_flow.TryRelease());
            Assert.AreEqual(DropState.Aiming, _flow.State);
        }
    }
}
