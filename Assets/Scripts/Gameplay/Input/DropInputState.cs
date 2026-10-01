using System;

namespace Coika.Gameplay
{
    /// <summary>
    /// The decisions of the drop input as plain C# (S-20), so they are tested without devices:
    /// which device was used last (the pointer or the keyboard), which presses count (those that begin over the UI
    /// do not) and what happens when the window loses focus. <see cref="PointerInputReader"/> reads the devices and
    /// feeds this class.
    /// </summary>
    public sealed class DropInputState : IDropInput
    {
        private bool _pointerLast;
        private bool _pointerInside;
        private bool _hasPointerPosition;
        private float _pointerWorldX;
        private float _keyboardAxis;
        private bool _pointerPressActive;
        private bool _keyboardPressActive;

        /// <inheritdoc />
        public event Action DropPressed;

        /// <inheritdoc />
        public event Action DropReleased;

        /// <inheritdoc />
        public bool HasPointer => _pointerLast && _pointerInside;

        /// <inheritdoc />
        public float PointerWorldX => _pointerWorldX;

        /// <inheritdoc />
        public float MoveAxis => _pointerLast ? 0f : _keyboardAxis;

        /// <summary>
        /// Reports where the pointer is. The pointer becomes the device last used when it moved since the last call.
        /// </summary>
        /// <param name="worldX">World X under the pointer.</param>
        /// <param name="isInsideScreen">Whether the pointer is inside the game screen.</param>
        public void PointerMoved(float worldX, bool isInsideScreen)
        {
            if (_hasPointerPosition && worldX != _pointerWorldX)
            {
                _pointerLast = true;
            }

            _hasPointerPosition = true;
            _pointerWorldX = worldX;
            _pointerInside = isInsideScreen;
        }

        /// <summary>
        /// Reports the keyboard axis. A non-zero axis makes the keyboard the device last used.
        /// </summary>
        /// <param name="axis">From -1 (left) to 1 (right).</param>
        public void KeyboardAxisChanged(float axis)
        {
            _keyboardAxis = axis;
            if (axis != 0f)
            {
                _pointerLast = false;
            }
        }

        /// <summary>
        /// Reports that a pointer press began. It makes the pointer the device last used. A press that begins over
        /// the UI is ignored: it raises nothing now and nothing when it ends.
        /// </summary>
        /// <param name="isOverUi">Whether the press began over a UI element.</param>
        public void PointerPressed(bool isOverUi)
        {
            _pointerLast = true;
            if (isOverUi)
            {
                return;
            }

            _pointerPressActive = true;
            DropPressed?.Invoke();
        }

        /// <summary>
        /// Reports that the pointer press ended. It raises <see cref="DropReleased"/> only for a press that was not
        /// ignored and only when the window still has focus.
        /// </summary>
        /// <param name="hasFocus">Whether the window has focus. A release caused by losing it never drops.</param>
        public void PointerReleased(bool hasFocus)
        {
            if (!_pointerPressActive)
            {
                return;
            }

            _pointerPressActive = false;
            if (hasFocus)
            {
                DropReleased?.Invoke();
            }
        }

        /// <summary>
        /// Reports that the drop key (Space) was pressed. It does not change which device is the last used, so a
        /// player who aims with the pointer and drops with Space keeps following the pointer.
        /// </summary>
        public void KeyboardDropPressed()
        {
            _keyboardPressActive = true;
            DropPressed?.Invoke();
        }

        /// <summary>
        /// Reports that the drop key was released. It raises <see cref="DropReleased"/> only for a press that began
        /// here and only when the window still has focus.
        /// </summary>
        /// <param name="hasFocus">Whether the window has focus.</param>
        public void KeyboardDropReleased(bool hasFocus)
        {
            if (!_keyboardPressActive)
            {
                return;
            }

            _keyboardPressActive = false;
            if (hasFocus)
            {
                DropReleased?.Invoke();
            }
        }

        /// <summary>
        /// Forgets every press in progress without raising anything, because the window lost focus or the reader
        /// was disabled mid-press.
        /// </summary>
        public void Cancel()
        {
            _pointerPressActive = false;
            _keyboardPressActive = false;
            _keyboardAxis = 0f;
        }
    }
}
