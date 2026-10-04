using System;

namespace Coika.Gameplay
{
    /// <summary>
    /// What the <see cref="DropController"/> needs from the player, whatever the device. The mouse, the touch screen
    /// and the keyboard implement it through <see cref="PointerInputReader"/>, so the gameplay code does not change
    /// with the device.
    /// </summary>
    public interface IDropInput
    {
        /// <summary>
        /// Whether the pointer is the device last used and is on screen, so <see cref="PointerWorldX"/> (plus the
        /// offset of <see cref="GetPointerOffset"/>) is the X to follow. A finger only counts while it is down.
        /// </summary>
        bool HasPointer { get; }

        /// <summary>
        /// World X under the pointer. Only meaningful while <see cref="HasPointer"/> is true. Add
        /// <see cref="GetPointerOffset"/> to get the X to follow.
        /// </summary>
        float PointerWorldX { get; }

        /// <summary>
        /// Sideways keyboard input, from -1 (left) to 1 (right). It is zero unless the keyboard is the device last
        /// used.
        /// </summary>
        float MoveAxis { get; }

        /// <summary>Raised when a press that can drop begins. Presses that begin over the UI never raise it.</summary>
        event Action DropPressed;

        /// <summary>
        /// Raised when a press that raised <see cref="DropPressed"/> ends. It is not raised when the window loses
        /// focus, so leaving the game mid-aim never drops the piece.
        /// </summary>
        event Action DropReleased;

        /// <summary>
        /// Raised when a press that raised <see cref="DropPressed"/> is cancelled (the OS takes the touch, a call
        /// comes in, the window loses focus). The piece goes back to hover and is not dropped.
        /// </summary>
        event Action DropCancelled;

        /// <summary>Raised once per press of the Back key (Escape, or the Android Back button).</summary>
        event Action BackPressed;

        /// <summary>
        /// Sideways distance between the pointer and the held piece to keep while following it. Ask it when a press
        /// begins. A touch keeps the place the piece had under the finger, or sits beside it when the Finger Offset
        /// setting is on. The mouse and the keyboard have no offset.
        /// </summary>
        /// <param name="heldX">World X of the held piece when the press began.</param>
        /// <returns>The value to add to <see cref="PointerWorldX"/> to get the X to follow.</returns>
        float GetPointerOffset(float heldX);
    }
}
