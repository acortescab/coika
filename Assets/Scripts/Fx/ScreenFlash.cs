using System;
using Coika.Data;
using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// The full-screen white flash: a Screen Space Overlay canvas with one white image, built once and reused, whose
    /// opacity is driven by a <see cref="FlashCore"/>. It never creates or destroys anything while playing. The
    /// prefab keeps the canvas group from blocking input.
    /// </summary>
    [AddComponentMenu("Coika/Fx/Screen Flash")]
    [DisallowMultipleComponent]
    public class ScreenFlash : MonoBehaviour, IScreenFlash
    {
        // Same-prefab object, so a direct reference is allowed (C-01).
        [SerializeField]
        private CanvasGroup _group;

        private FlashCore _core;
        private Func<double> _clock;

        /// <summary>
        /// Prepares the flash and hides the overlay.
        /// </summary>
        /// <param name="config">Peak opacity and the most flashes per second.</param>
        /// <param name="clock">Seconds that do not depend on the time scale.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        /// <exception cref="InvalidOperationException">The canvas group is not assigned.</exception>
        public void Initialize(FeedbackConfig config, Func<double> clock)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (_group == null)
            {
                throw new InvalidOperationException("The ScreenFlash needs a CanvasGroup.");
            }

            _core = new FlashCore(config.ScreenFx.MaxFlashesPerSecond, config.ScreenFx.ScreenFlashPeakAlpha);
            _group.alpha = 0f;
        }

        /// <summary>
        /// Starts a flash unless it would exceed the allowed rate. Does nothing before <see cref="Initialize"/>.
        /// </summary>
        /// <param name="duration">Seconds the flash takes to fade out.</param>
        public void Flash(float duration)
        {
            _core?.TryFlash(duration, _clock());
        }

        /// <summary>
        /// Stops the flash and hides the overlay.
        /// </summary>
        public void Clear()
        {
            _core?.Clear();
            if (_group != null)
            {
                _group.alpha = 0f;
            }
        }

        /// <summary>
        /// Sets the opacity of this frame.
        /// </summary>
        private void LateUpdate()
        {
            if (_core == null)
            {
                return;
            }

            var alpha = _core.Alpha(_clock());
            if (_group.alpha != alpha)
            {
                _group.alpha = alpha;
            }
        }
    }
}
