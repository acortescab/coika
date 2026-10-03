using System;
using Coika.Gameplay;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Test double for <see cref="IDropInput"/>: the test sets the pointer and the keyboard axis and raises the
    /// presses by hand, so the controller is tested without devices.
    /// </summary>
    public class FakeDropInput : IDropInput
    {
        /// <inheritdoc />
        public event Action DropPressed;

        /// <inheritdoc />
        public event Action DropReleased;

        /// <inheritdoc />
        public event Action DropCancelled;

        /// <inheritdoc />
        public event Action BackPressed;

        /// <summary>Value returned by <see cref="GetPointerOffset"/>.</summary>
        public float PointerOffset { get; set; }

        /// <inheritdoc />
        public bool HasPointer { get; set; }

        /// <inheritdoc />
        public float PointerWorldX { get; set; }

        /// <inheritdoc />
        public float MoveAxis { get; set; }

        /// <summary>Number of listeners of the drop events, to check that they are removed.</summary>
        public int ListenerCount => (DropPressed?.GetInvocationList().Length ?? 0) + (DropReleased?.GetInvocationList().Length ?? 0)
            + (DropCancelled?.GetInvocationList().Length ?? 0);

        /// <inheritdoc />
        public float GetPointerOffset(float heldX)
        {
            return PointerOffset;
        }

        /// <summary>Raises <see cref="DropCancelled"/>.</summary>
        public void RaiseCancelled()
        {
            DropCancelled?.Invoke();
        }

        /// <summary>Raises <see cref="BackPressed"/>.</summary>
        public void RaiseBack()
        {
            BackPressed?.Invoke();
        }

        /// <summary>Raises <see cref="DropPressed"/>.</summary>
        public void RaisePressed()
        {
            DropPressed?.Invoke();
        }

        /// <summary>Raises <see cref="DropReleased"/>.</summary>
        public void RaiseReleased()
        {
            DropReleased?.Invoke();
        }

        /// <summary>Raises a press and then its release, as a quick click does.</summary>
        public void Click()
        {
            RaisePressed();
            RaiseReleased();
        }
    }
}
