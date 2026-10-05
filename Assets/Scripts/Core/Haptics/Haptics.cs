using System;
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// Haptics wrapper (GDD §14.3). Applies the Haptics setting and the throttle, then forwards to the platform
    /// backend. The time comes from an injected clock, never from the real one (S-63). Any backend failure is
    /// logged once and disables the wrapper for the rest of the session.
    /// </summary>
    public sealed class Haptics : IHaptics
    {
        /// <summary>Shortest time between two haptics, in seconds. A heavier kind may still cut into it.</summary>
        public const double MIN_INTERVAL = 0.05;

        // Strength of each kind, indexed by the enum value (Light, VeryLight, Medium, Heavy, Long).
        private static readonly int[] RANKS = { 1, 0, 2, 3, 4 };

        private readonly IHapticsBackend _backend;
        private readonly SettingsService _settings;
        private readonly Func<double> _clock;
        private double _lastPlayed = double.NegativeInfinity;
        private int _lastRank = -1;
        private bool _disabled;

        /// <summary>
        /// Creates the wrapper.
        /// </summary>
        /// <param name="backend">The platform side.</param>
        /// <param name="settings">Source of the Haptics setting, or null to keep haptics always on.</param>
        /// <param name="clock">Returns the current time in seconds.</param>
        /// <exception cref="ArgumentNullException">The backend or the clock is null.</exception>
        public Haptics(IHapticsBackend backend, SettingsService settings, Func<double> clock)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _settings = settings;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>True once a backend call failed: the wrapper then ignores every request.</summary>
        public bool IsDisabled => _disabled;

        /// <summary>
        /// Plays the effect unless haptics are off, the wrapper is disabled, or a haptic of the same or a higher
        /// strength played less than <see cref="MIN_INTERVAL"/> ago.
        /// </summary>
        /// <param name="kind">The effect to play.</param>
        public void Play(HapticKind kind)
        {
            if (_disabled || (_settings != null && !_settings.Haptics))
            {
                return;
            }

            var rank = RANKS[(int)kind];
            var now = _clock();
            if (now - _lastPlayed < MIN_INTERVAL && rank <= _lastRank)
            {
                return;
            }

            try
            {
                _backend.Play(kind);
            }
            catch (Exception e)
            {
                _disabled = true;
                Debug.LogWarning($"Haptics disabled for this session: {e.Message}");
                return;
            }

            _lastPlayed = now;
            _lastRank = rank;
        }
    }
}
