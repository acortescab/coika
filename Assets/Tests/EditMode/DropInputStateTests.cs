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

        /// <summary>
        /// A touch without the finger offset has no offset: the piece heads to the finger at the capped speed.
        /// </summary>
        [Test]
        public void GetPointerOffset_TouchWithoutFingerOffset_IsZero()
        {
            _state.PointerMoved(2f, true);
            _state.PointerMoved(3f, true);
            _state.PointerPressed(false, true);

            Assert.AreEqual(0f, _state.GetPointerOffset(2f));
        }

        /// <summary>
        /// The mouse never has an offset.
        /// </summary>
        [Test]
        public void GetPointerOffset_Mouse_IsZero()
        {
            _state.PointerMoved(2f, true);
            _state.PointerMoved(3f, true);
            _state.PointerPressed(false);

            Assert.AreEqual(0f, _state.GetPointerOffset(-5f));
        }

        /// <summary>
        /// With the finger offset on, a right-handed player gets the piece to the left of the finger.
        /// </summary>
        [Test]
        public void GetPointerOffset_FingerOffsetRightHanded_PutsThePieceLeftOfTheFinger()
        {
            _state.ConfigureFingerOffset(true, false, 2f);
            _state.PointerMoved(2f, true);
            _state.PointerMoved(3f, true);
            _state.PointerPressed(false, true);

            Assert.AreEqual(-2f, _state.GetPointerOffset(9f));
        }

        /// <summary>
        /// Left-handed only mirrors the side of the finger offset.
        /// </summary>
        [Test]
        public void GetPointerOffset_FingerOffsetLeftHanded_PutsThePieceRightOfTheFinger()
        {
            _state.ConfigureFingerOffset(true, true, 2f);
            _state.PointerMoved(2f, true);
            _state.PointerMoved(3f, true);
            _state.PointerPressed(false, true);

            Assert.AreEqual(2f, _state.GetPointerOffset(9f));
        }

        /// <summary>
        /// A finger is only followed while it is down: a touch screen has no hover.
        /// </summary>
        [Test]
        public void HasPointer_TouchAfterRelease_IsFalse()
        {
            _state.PointerMoved(2f, true);
            _state.PointerMoved(3f, true);
            _state.PointerPressed(false, true);
            Assert.IsTrue(_state.HasPointer);

            _state.PointerReleased(true);

            Assert.IsFalse(_state.HasPointer);
        }

        /// <summary>
        /// A touch that starts over the UI never moves the piece.
        /// </summary>
        [Test]
        public void HasPointer_TouchPressedOverTheUi_IsFalse()
        {
            _state.PointerMoved(2f, true);
            _state.PointerMoved(3f, true);
            _state.PointerPressed(true, true);

            Assert.IsFalse(_state.HasPointer);
            Assert.AreEqual(0, _pressed);
        }

        /// <summary>
        /// A press that began on the play area still drops on release even when the finger ends over the UI: only
        /// where the press began decides (documented rule of issue #27).
        /// </summary>
        [Test]
        public void PointerReleased_PressBeganOnThePlayAreaAndEndsOverTheUi_Drops()
        {
            _state.PointerPressed(false, true);

            _state.PointerReleased(true);

            Assert.AreEqual(1, _released);
        }

        /// <summary>
        /// A touch the system cancels raises the cancel event and never a release.
        /// </summary>
        [Test]
        public void PointerCancelled_PressInProgress_RaisesCancelAndNoRelease()
        {
            var cancelled = 0;
            _state.DropCancelled += () => cancelled++;
            _state.PointerPressed(false, true);

            _state.PointerCancelled();
            _state.PointerReleased(true);

            Assert.AreEqual(1, cancelled);
            Assert.AreEqual(0, _released);
        }

        /// <summary>
        /// Cancelling a press that was ignored, or that does not exist, raises nothing.
        /// </summary>
        [Test]
        public void PointerCancelled_WithoutAnActivePress_RaisesNothing()
        {
            var cancelled = 0;
            _state.DropCancelled += () => cancelled++;
            _state.PointerPressed(true, true);

            _state.PointerCancelled();

            Assert.AreEqual(0, cancelled);
        }

        /// <summary>
        /// Losing focus mid-press raises the cancel event once, so the piece goes back to hover.
        /// </summary>
        [Test]
        public void Cancel_PressInProgress_RaisesCancelOnce()
        {
            var cancelled = 0;
            _state.DropCancelled += () => cancelled++;
            _state.PointerPressed(false, true);

            _state.Cancel();
            _state.Cancel();

            Assert.AreEqual(1, cancelled);
            Assert.AreEqual(0, _released);
        }

        /// <summary>
        /// Each press of the Back key raises the event exactly once.
        /// </summary>
        [Test]
        public void BackKeyPressed_EachPress_RaisesBackOnce()
        {
            var back = 0;
            _state.BackPressed += () => back++;

            _state.BackKeyPressed();

            Assert.AreEqual(1, back);
        }

        /// <summary>
        /// After a finger has been used, moving the mouse hands the control back to the mouse without a click.
        /// </summary>
        [Test]
        public void HasPointer_MouseMovesAfterATouchPress_IsTrue()
        {
            _state.PointerMoved(2f, true);
            _state.PointerMoved(3f, true, true);
            _state.PointerPressed(false, true);
            _state.PointerReleased(true);
            Assert.IsFalse(_state.HasPointer, "A finger that is up is not followed.");

            _state.PointerMoved(7f, true);

            Assert.IsTrue(_state.HasPointer);
            Assert.AreEqual(7f, _state.PointerWorldX);
            Assert.AreEqual(0f, _state.GetPointerOffset(1f), "The mouse has no offset.");
        }
    }
}
