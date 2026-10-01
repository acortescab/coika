using Coika.Gameplay;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the decisions of <see cref="DropInputState"/> (issue #6): which device is the last used, which presses
    /// count and what happens when the window loses focus.
    /// </summary>
    public class DropInputStateTests
    {
        private DropInputState _state;
        private int _pressed;
        private int _released;

        /// <summary>
        /// Creates a state and counts the events it raises.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _state = new DropInputState();
            _pressed = 0;
            _released = 0;
            _state.DropPressed += () => _pressed++;
            _state.DropReleased += () => _released++;
        }

        /// <summary>
        /// Until the pointer moves or presses there is no pointer to follow.
        /// </summary>
        [Test]
        public void HasPointer_BeforeAnyMovement_IsFalse()
        {
            _state.PointerMoved(3f, true);

            Assert.IsFalse(_state.HasPointer);
        }

        /// <summary>
        /// After the pointer moves inside the screen it is followed.
        /// </summary>
        [Test]
        public void PointerMoved_AfterAMovement_MakesThePointerTheTarget()
        {
            _state.PointerMoved(3f, true);
            _state.PointerMoved(4f, true);

            Assert.IsTrue(_state.HasPointer);
            Assert.AreEqual(4f, _state.PointerWorldX);
        }

        /// <summary>
        /// A pointer outside the screen is not followed.
        /// </summary>
        [Test]
        public void HasPointer_WhenThePointerLeavesTheScreen_IsFalse()
        {
            _state.PointerMoved(3f, true);
            _state.PointerMoved(4f, true);

            _state.PointerMoved(40f, false);

            Assert.IsFalse(_state.HasPointer);
        }

        /// <summary>
        /// A key makes the keyboard the last device: the axis counts and the pointer is not followed.
        /// </summary>
        [Test]
        public void KeyboardAxisChanged_WithAKey_MakesTheKeyboardTheLastDevice()
        {
            _state.PointerMoved(3f, true);
            _state.PointerMoved(4f, true);

            _state.KeyboardAxisChanged(1f);

            Assert.IsFalse(_state.HasPointer);
            Assert.AreEqual(1f, _state.MoveAxis);
        }

        /// <summary>
        /// Moving the pointer again after a key hands the control back to the pointer, and the axis is ignored.
        /// </summary>
        [Test]
        public void PointerMoved_AfterTheKeyboard_TakesTheControlBack()
        {
            _state.PointerMoved(3f, true);
            _state.KeyboardAxisChanged(1f);

            _state.PointerMoved(5f, true);

            Assert.IsTrue(_state.HasPointer);
            Assert.AreEqual(0f, _state.MoveAxis);
        }

        /// <summary>
        /// A pointer that does not move does not take the control back from the keyboard.
        /// </summary>
        [Test]
        public void PointerMoved_WithTheSamePosition_DoesNotTakeTheControlBack()
        {
            _state.PointerMoved(3f, true);
            _state.KeyboardAxisChanged(1f);

            _state.PointerMoved(3f, true);

            Assert.IsFalse(_state.HasPointer);
        }

        /// <summary>
        /// Releasing the key leaves the keyboard as the last device with no movement.
        /// </summary>
        [Test]
        public void KeyboardAxisChanged_ToZero_StopsTheMovement()
        {
            _state.KeyboardAxisChanged(1f);

            _state.KeyboardAxisChanged(0f);

            Assert.AreEqual(0f, _state.MoveAxis);
        }

        /// <summary>
        /// A pointer press and its release raise both events once.
        /// </summary>
        [Test]
        public void PointerPressed_ThenReleased_RaisesBothEvents()
        {
            _state.PointerPressed(false);
            _state.PointerReleased(true);

            Assert.AreEqual(1, _pressed);
            Assert.AreEqual(1, _released);
        }

        /// <summary>
        /// A press that begins over the UI raises nothing, neither when it begins nor when it ends.
        /// </summary>
        [Test]
        public void PointerPressed_OverTheUi_RaisesNothing()
        {
            _state.PointerPressed(true);
            _state.PointerReleased(true);

            Assert.AreEqual(0, _pressed);
            Assert.AreEqual(0, _released);
        }

        /// <summary>
        /// A release with no press raises nothing.
        /// </summary>
        [Test]
        public void PointerReleased_WithoutAPress_RaisesNothing()
        {
            _state.PointerReleased(true);

            Assert.AreEqual(0, _released);
        }

        /// <summary>
        /// A press that ends because the window lost focus does not drop.
        /// </summary>
        [Test]
        public void PointerReleased_WithoutFocus_DoesNotRaiseTheRelease()
        {
            _state.PointerPressed(false);
            _state.PointerReleased(false);

            Assert.AreEqual(1, _pressed);
            Assert.AreEqual(0, _released);
        }

        /// <summary>
        /// Cancel forgets the press in progress, so its release never drops.
        /// </summary>
        [Test]
        public void Cancel_DuringAPress_StopsTheReleaseFromDropping()
        {
            _state.PointerPressed(false);

            _state.Cancel();
            _state.PointerReleased(true);

            Assert.AreEqual(0, _released);
        }

        /// <summary>
        /// Cancel also stops a key that was held, so the piece does not keep moving.
        /// </summary>
        [Test]
        public void Cancel_WithAKeyHeld_StopsTheMovement()
        {
            _state.KeyboardAxisChanged(1f);

            _state.Cancel();

            Assert.AreEqual(0f, _state.MoveAxis);
        }

        /// <summary>
        /// The drop key raises the press and the release.
        /// </summary>
        [Test]
        public void KeyboardDrop_PressedThenReleased_RaisesBothEvents()
        {
            _state.KeyboardDropPressed();
            _state.KeyboardDropReleased(true);

            Assert.AreEqual(1, _pressed);
            Assert.AreEqual(1, _released);
        }

        /// <summary>
        /// Dropping with Space while aiming with the pointer keeps following the pointer.
        /// </summary>
        [Test]
        public void KeyboardDropPressed_WhileUsingThePointer_KeepsFollowingThePointer()
        {
            _state.PointerMoved(3f, true);
            _state.PointerMoved(4f, true);

            _state.KeyboardDropPressed();

            Assert.IsTrue(_state.HasPointer);
        }

        /// <summary>
        /// A key release with no press raises nothing.
        /// </summary>
        [Test]
        public void KeyboardDropReleased_WithoutAPress_RaisesNothing()
        {
            _state.KeyboardDropReleased(true);

            Assert.AreEqual(0, _released);
        }
    }
}
