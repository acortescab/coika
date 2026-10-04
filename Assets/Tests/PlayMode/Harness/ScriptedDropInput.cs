using System;
using Coika.Gameplay;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Input the simulation harness controls by hand: the runner sets the pointer X and raises the press and the
    /// release itself, so no device or real frame is involved. Its listeners can be counted with <see cref="EventListeners"/>, so the lifecycle tests can
    /// check that a run leaves none behind.
    /// </summary>
    public sealed class ScriptedDropInput : IDropInput
    {
        /// <inheritdoc />
        public event Action DropPressed;

        /// <inheritdoc />
        public event Action DropReleased;

        /// <inheritdoc />
        public event Action DropCancelled;

        /// <inheritdoc />
        public event Action BackPressed;

        /// <inheritdoc />
        public bool HasPointer { get; set; }

        /// <inheritdoc />
        public float PointerWorldX { get; set; }

        /// <inheritdoc />
        public float MoveAxis { get; set; }

        /// <inheritdoc />
        public float GetPointerOffset(float heldX)
        {
            return 0f;
        }

        /// <summary>Raises <see cref="DropCancelled"/>.</summary>
        public void Cancel()
        {
            DropCancelled?.Invoke();
        }

        /// <summary>Raises <see cref="BackPressed"/>.</summary>
        public void Back()
        {
            BackPressed?.Invoke();
        }

        /// <summary>Raises <see cref="DropPressed"/>: a finger goes down or the drop key is pressed.</summary>
        public void Press()
        {
            DropPressed?.Invoke();
        }

        /// <summary>Raises <see cref="DropReleased"/>: the finger lifts or the drop key is released.</summary>
        public void Release()
        {
            DropReleased?.Invoke();
        }

        /// <summary>Raises a press and then its release, as a quick click does.</summary>
        public void Click()
        {
            DropPressed?.Invoke();
            DropReleased?.Invoke();
        }
    }
}
