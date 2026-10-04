using System;

namespace Coika.Gameplay
{
    /// <summary>
    /// The decisions of the drop input as plain C# (S-20), so they are tested without devices:
    /// which device was used last (the pointer or the keyboard), which presses count (those that begin over the UI
    /// do not), what happens when a press is cancelled or the window loses focus, and where a finger holds the
    /// piece (the offset kept from the press start, or the Finger Offset side). <see cref="PointerInputReader"/>
    /// reads the devices and feeds this class.
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
        private bool _pointerIsTouch;
        private bool _fingerOffsetEnabled;
        private float _fingerShift;

        /// <inheritdoc />
        public event Action DropPressed;

        /// <inheritdoc />
        public event Action DropReleased;

        /// <inheritdoc />
        public event Action DropCancelled;

        /// <inheritdoc />
        public event Action BackPressed;

        /// <summary>
        /// Whether the pointer is the device last used and is on screen. A finger only counts while its press is in
        /// progress, because a touch screen has no hover: the piece stays where it is between touches.
        /// </summary>
        public bool HasPointer => _pointerLast && _pointerInside && (!_pointerIsTouch || _pointerPressActive);

        /// <inheritdoc />
        public float PointerWorldX => _pointerWorldX;

        /// <inheritdoc />
        public float MoveAxis => _pointerLast ? 0f : _keyboardAxis;

        /// <summary>
        /// Reports where the pointer is. The pointer becomes the device last used when it moved since the last call.
        /// </summary>
        /// <param name="worldX">World X under the pointer.</param>
        /// <param name="isInsideScreen">Whether the pointer is inside the game screen.</param>
        /// <param name="fromTouch">Whether a finger is down, so the position comes from the touch screen and not the mouse.</param>
        /// <param name="screenMoved">
        /// Whether the pointer moved on the screen. When null, a change of <paramref name="worldX"/> counts, but a
        /// camera that reframes moves the world X of a pointer that stayed still, so the reader passes it.
        /// </param>
        public void PointerMoved(float worldX, bool isInsideScreen, bool fromTouch = false, bool? screenMoved = null)
        {
            if (_hasPointerPosition && screenMoved.GetValueOrDefault(worldX != _pointerWorldX))
            {
                _pointerLast = true;
                _pointerIsTouch = fromTouch;
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
        /// Sets where the held piece sits relative to the finger. With the setting off, a touch keeps the offset the
        /// piece had when the finger went down, so the piece never jumps.
        /// </summary>
        /// <param name="enabled">Whether the Finger Offset setting is on.</param>
        /// <param name="leftHanded">Whether the Left-handed setting is on. It only chooses the side.</param>
        /// <param name="distance">Sideways distance in world units between the finger and the piece.</param>
        public void ConfigureFingerOffset(bool enabled, bool leftHanded, float distance)
        {
            _fingerOffsetEnabled = enabled;
            _fingerShift = leftHanded ? distance : -distance;
        }

        /// <inheritdoc />
        public float GetPointerOffset(float heldX)
        {
            if (!_pointerIsTouch || !_pointerPressActive)
            {
                return 0f;
            }

            return _fingerOffsetEnabled ? _fingerShift : 0f;
        }

        /// <summary>
        /// Reports that the Back key was pressed. Each call raises <see cref="BackPressed"/> once.
        /// </summary>
        public void BackKeyPressed()
        {
            BackPressed?.Invoke();
        }

        /// <summary>
        /// Reports that a pointer press began. It makes the pointer the device last used. A press that begins over
        /// the UI is ignored: it raises nothing now and nothing when it ends.
        /// </summary>
        /// <param name="isOverUi">Whether the press began over a UI element.</param>
        /// <param name="isTouch">Whether the press is a finger on the touch screen and not the mouse.</param>
        public void PointerPressed(bool isOverUi, bool isTouch = false)
        {
            _pointerLast = true;
            _pointerIsTouch = isTouch;
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
        /// Reports that the pointer press was cancelled by the system, for instance an OS gesture took the touch. It
        /// raises <see cref="DropCancelled"/> for a press that was not ignored and never <see cref="DropReleased"/>.
        /// </summary>
        public void PointerCancelled()
        {
            if (!_pointerPressActive)
            {
                return;
            }

            _pointerPressActive = false;
            DropCancelled?.Invoke();
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
        /// Forgets every press in progress because the window lost focus or the reader was disabled mid-press. It
        /// never raises <see cref="DropReleased"/>; it raises <see cref="DropCancelled"/> once if a press was in progress.
        /// </summary>
        public void Cancel()
        {
            var wasPressed = _pointerPressActive || _keyboardPressActive;
            _pointerPressActive = false;
            _keyboardPressActive = false;
            _keyboardAxis = 0f;
            if (wasPressed)
            {
                DropCancelled?.Invoke();
            }
        }
    }
}
