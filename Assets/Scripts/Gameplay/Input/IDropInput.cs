using System;

namespace Coika.Gameplay
{
    /// <summary>
    /// What the <see cref="DropController"/> needs from the player, whatever the device. The desktop pointer and
    /// keyboard implement it now (<see cref="PointerInputReader"/>); touch plugs in later by implementing it too,
    /// so the gameplay code does not change.
    /// </summary>
    public interface IDropInput
    {
        /// <summary>
        /// Whether the pointer is the device last used and is on screen, so <see cref="PointerWorldX"/> is the X to
        /// follow.
        /// </summary>
        bool HasPointer { get; }

        /// <summary>World X under the pointer. Only meaningful while <see cref="HasPointer"/> is true.</summary>
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
    }
}
