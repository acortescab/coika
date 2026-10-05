using System;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The rules of the drop as plain C# (S-20): the substate, the cooldown and the
    /// maths of the sideways follow. It knows nothing of pieces, input or physics, so it is tested with exact time
    /// steps; <see cref="DropController"/> applies its results to the held piece.
    /// <para>
    /// A release only counts when its press began while the state was <see cref="DropState.Aiming"/>. A press that
    /// begins during the cooldown is therefore ignored even if it is released after the cooldown ends.
    /// </para>
    /// </summary>
    public sealed class DropFlow
    {
        /// <summary>Time left below which the cooldown counts as over, so float error cannot add a step.</summary>
        private const float COOLDOWN_EPSILON = 1e-5f;

        private readonly float _cooldown;

        private float _remaining;
        private bool _armed;

        /// <summary>
        /// Creates the flow in the <see cref="DropState.Aiming"/> state.
        /// </summary>
        /// <param name="cooldown">Seconds between a release and the next piece being controllable.</param>
        /// <exception cref="ArgumentOutOfRangeException">The cooldown is negative.</exception>
        public DropFlow(float cooldown)
        {
            if (cooldown < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldown), "The cooldown cannot be negative.");
            }

            _cooldown = cooldown;
        }

        /// <summary>The current substate.</summary>
        public DropState State { get; private set; }

        /// <summary>Whether a press began while aiming and has not been released, cancelled or dropped yet.</summary>
        public bool IsPressing => _armed;

        /// <summary>
        /// Goes back to <see cref="DropState.Aiming"/> with no press pending, as at the start
        /// of a run.
        /// </summary>
        public void Begin()
        {
            EndCooldown();
            _remaining = 0f;
            _armed = false;
        }

        /// <summary>
        /// Moves the cooldown forward. Does nothing while aiming.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last call.</param>
        /// <returns>True on the call in which the cooldown ends and the state becomes <see cref="DropState.Aiming"/>.</returns>
        public bool Tick(float deltaTime)
        {
            if (State != DropState.Dropping)
            {
                return false;
            }

            _remaining -= deltaTime;

            if (_remaining > COOLDOWN_EPSILON)
            {
                return false;
            }

            EndCooldown();
            return true;
        }

        /// <summary>
        /// Notes that a press began. It only arms a release while aiming; during the cooldown it is ignored.
        /// </summary>
        public void Press()
        {
            if (State == DropState.Aiming)
            {
                _armed = true;
            }
        }

        /// <summary>
        /// Forgets the press that was armed, so its release (if one ever comes) drops nothing. The piece goes back to
        /// hovering in the <see cref="DropState.Aiming"/> state.
        /// </summary>
        public void CancelPress()
        {
            _armed = false;
        }

        /// <summary>
        /// Tries to release the held piece. It succeeds when aiming and a press began while aiming. It then starts
        /// the cooldown.
        /// </summary>
        /// <returns>True when the piece must be dropped now.</returns>
        public bool TryRelease()
        {
            var canRelease = State == DropState.Aiming && _armed;
            _armed = false;
            if (!canRelease)
            {
                return false;
            }

            State = DropState.Dropping;
            _remaining = _cooldown;

            if (_cooldown <= COOLDOWN_EPSILON)
            {
                EndCooldown();
            }

            return true;
        }

        /// <summary>
        /// Keeps a piece of the given radius fully inside the interval. A piece wider than the interval is centred.
        /// </summary>
        /// <param name="x">Wanted centre of the piece.</param>
        /// <param name="radius">Radius of the piece.</param>
        /// <param name="minX">Left edge of the interior.</param>
        /// <param name="maxX">Right edge of the interior.</param>
        /// <returns>The centre, kept inside.</returns>
        public static float ClampX(float x, float radius, float minX, float maxX)
        {
            var low = minX + radius;
            var high = maxX - radius;
            return low > high ? (minX + maxX) * 0.5f : Mathf.Clamp(x, low, high);
        }

        /// <summary>
        /// Moves toward a target without going faster than the maximum speed, so a pointer that jumps across the
        /// screen in one frame never teleports the piece.
        /// </summary>
        /// <param name="current">Current centre.</param>
        /// <param name="target">Wanted centre.</param>
        /// <param name="maxSpeed">Fastest allowed speed, in units per second.</param>
        /// <param name="deltaTime">Seconds to move for.</param>
        /// <returns>The new centre.</returns>
        public static float Follow(float current, float target, float maxSpeed, float deltaTime)
        {
            return Mathf.MoveTowards(current, target, maxSpeed * deltaTime);
        }

        /// <summary>
        /// Goes back to aiming.
        /// </summary>
        private void EndCooldown()
        {
            State = DropState.Aiming;
        }
    }
}
