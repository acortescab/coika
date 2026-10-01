using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The dashed line drawn at the jar's Danger Line (GDD §3.1). It is hidden by default and can pulse red to
    /// warn the player that a piece is close to overflowing. <see cref="JarBuilder"/> creates and sizes it; the
    /// overflow logic (issue #9) decides when to show it and make it pulse.
    /// <para>
    /// <see cref="Update"/> only runs while the line is both visible and pulsing, so an idle line costs nothing.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class DangerLine : MonoBehaviour
    {
        /// <summary>
        /// Speed of the red pulse. Soft is the accessibility alternative.
        /// </summary>
        public enum PulseRate
        {
            /// <summary>Default pulse, 4 cycles per second.</summary>
            Fast,

            /// <summary>Gentler pulse, 2 cycles per second, for players who prefer less flashing.</summary>
            Soft
        }

        /// <summary>Pulse cycles per second of <see cref="PulseRate.Fast"/>.</summary>
        public const float FAST_PULSE_HZ = 4f;

        /// <summary>Pulse cycles per second of <see cref="PulseRate.Soft"/>.</summary>
        public const float SOFT_PULSE_HZ = 2f;

        private const int SORTING_ORDER = 10; // Above the jar walls and the pieces

        [SerializeField]
        private Color _idleColor = new(0.78f, 0.82f, 0.86f, 1f);
        [SerializeField]
        private Color _pulseColor = new(0.95f, 0.25f, 0.25f, 1f);

        private SpriteRenderer _renderer;
        private bool _isPulsing;
        private PulseRate _pulseRate = PulseRate.Fast;

        /// <summary>Colour of the line while it is not pulsing, and the low point of the pulse.</summary>
        public Color IdleColor => _idleColor;

        /// <summary>Red the line reaches at the peak of the pulse.</summary>
        public Color PulseColor => _pulseColor;

        /// <summary>Whether the line is drawn.</summary>
        public bool IsVisible => Renderer.enabled;

        /// <summary>Whether the pulse was requested. The line only animates while it is also visible.</summary>
        public bool IsPulsing => _isPulsing;

        /// <summary>Pulse cycles per second of the current <see cref="PulseRate"/>.</summary>
        public float PulseHz => _pulseRate == PulseRate.Soft ? SOFT_PULSE_HZ : FAST_PULSE_HZ;

        /// <summary>The sprite renderer of this object, found once and cached.</summary>
        private SpriteRenderer Renderer
        {
            get
            {
                if (_renderer == null)
                    _renderer = GetComponent<SpriteRenderer>();

                return _renderer;
            }
        }

        /// <summary>
        /// Shows or hides the line.
        /// </summary>
        /// <param name="visible">True to draw the line.</param>
        public void SetVisible(bool visible)
        {
            Renderer.enabled = visible;
            RefreshUpdateState();
        }

        /// <summary>
        /// Starts or stops the red pulse. Stopping it puts the line back to its idle colour.
        /// </summary>
        /// <param name="pulse">True to pulse.</param>
        public void SetPulse(bool pulse)
        {
            _isPulsing = pulse;

            if (!pulse)
                Renderer.color = _idleColor;

            RefreshUpdateState();
        }

        /// <summary>
        /// Chooses the speed of the pulse. Wired to the accessibility setting in M2.
        /// </summary>
        /// <param name="rate">Fast (4 Hz, default) or Soft (2 Hz).</param>
        public void SetPulseRate(PulseRate rate)
        {
            _pulseRate = rate;
        }

        /// <summary>
        /// Colour of the line at a given time while it pulses: a sine wave between the idle colour and red at the
        /// current pulse rate. It is a pure function, so it can be tested without running frames.
        /// </summary>
        /// <param name="time">Time in seconds.</param>
        /// <returns>The colour the line should have at that time.</returns>
        public Color EvaluateColor(float time)
        {
            var wave = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * PulseHz * time);
            return Color.Lerp(_idleColor, _pulseColor, wave);
        }

        /// <summary>
        /// Sets the line as tall as one reference pixel and as wide as the jar interior, tiling the dash sprite.
        /// Called by <see cref="JarBuilder"/> every time it builds.
        /// </summary>
        /// <param name="width">Width of the line in world units.</param>
        internal void Configure(float width)
        {
            var spriteRenderer = Renderer;
            spriteRenderer.drawMode = SpriteDrawMode.Tiled;
            spriteRenderer.size = new Vector2(width, 1f / TierDefinition.PixelsPerUnit);
            spriteRenderer.sortingOrder = SORTING_ORDER;

            if (!_isPulsing)
                spriteRenderer.color = _idleColor;
        }

        /// <summary>
        /// Animates the colour. Runs only while the line is visible and pulsing; unscaled time keeps the pulse
        /// going when the game freezes physics at game over.
        /// </summary>
        private void Update()
        {
            Renderer.color = EvaluateColor(Time.time);
        }

        /// <summary>
        /// Enables <see cref="Update"/> only when it has something to do.
        /// </summary>
        private void RefreshUpdateState()
        {
            enabled = Renderer.enabled && _isPulsing;
        }
    }
}
