using System;
using Coika.Data;
using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// Shakes the camera by moving the camera rig, the parent of the camera, in <c>LateUpdate</c>. The rig sits at
    /// its rest position and the camera inside it is framed by <c>JarCameraFramer</c>, so the framing is never
    /// touched. The offset comes from a <see cref="ShakeCore"/>: whole steps of 1/16 unit, capped, and exactly the
    /// rest position when no shake runs.
    /// </summary>
    [AddComponentMenu("Coika/Fx/Screen Shake")]
    [DisallowMultipleComponent]
    public class ScreenShake : MonoBehaviour, IScreenShake
    {
        private const int SEED = 7331;

        private ShakeCore _core;
        private Func<double> _clock;
        private Vector3 _rest;
        private Vector3 _applied;

        /// <summary>
        /// Prepares the shake and remembers the current position of the rig as its rest position.
        /// </summary>
        /// <param name="config">Cap of the combined shake.</param>
        /// <param name="clock">Seconds that do not depend on the time scale.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public void Initialize(FeedbackConfig config, Func<double> clock)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _core = new ShakeCore(FeedbackConfig.SHAKE_SLOTS, config.ShakeMaxAmplitude, SEED);
            _rest = transform.position;
            _applied = _rest;
        }

        /// <summary>
        /// Starts a shake. Does nothing before <see cref="Initialize"/>.
        /// </summary>
        /// <param name="amplitude">Largest offset in world units at the start.</param>
        /// <param name="duration">Seconds until the shake is over.</param>
        public void Shake(float amplitude, float duration)
        {
            _core?.Add(amplitude, duration, _clock());
        }

        /// <summary>
        /// Stops every shake and puts the rig back at its rest position.
        /// </summary>
        public void Clear()
        {
            _core?.Clear();
            Place(Vector2.zero);
        }

        /// <summary>
        /// Moves the rig by the offset of this frame.
        /// </summary>
        private void LateUpdate()
        {
            if (_core != null)
            {
                Place(_core.Evaluate(_clock()));
            }
        }

        /// <summary>
        /// Puts the rig at its rest position plus an offset, writing the transform only when it changes.
        /// </summary>
        /// <param name="offset">Offset in world units.</param>
        private void Place(Vector2 offset)
        {
            var target = new Vector3(_rest.x + offset.x, _rest.y + offset.y, _rest.z);
            if (target != _applied)
            {
                _applied = target;
                transform.position = target;
            }
        }
    }
}
