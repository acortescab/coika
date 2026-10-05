using System;
using Coika.Data;
using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// The maths of the screen shake, with no Unity objects: a fixed set of slots, each a shake that fades out
    /// linearly. The offset is the sum of the slots, capped per axis and snapped to a step of 1/16 world unit, so
    /// the Pixel Perfect Camera keeps the pixel look, and it is exactly zero when no shake is running. The
    /// directions come from a seeded <see cref="System.Random"/> (S-63), so the same requests give the same offsets.
    /// It allocates nothing after it is built.
    /// </summary>
    public sealed class ShakeCore
    {
        private readonly float[] _amplitude;
        private readonly double[] _start;
        private readonly double[] _end;
        private readonly float _maxAmplitude;
        private readonly System.Random _random;

        /// <summary>
        /// Creates a core with every slot free.
        /// </summary>
        /// <param name="slots">How many shakes may run at once.</param>
        /// <param name="maxAmplitude">Cap in world units of the combined offset on each axis, rounded down to a multiple of 1/16.</param>
        /// <param name="seed">Seed of the direction generator.</param>
        /// <exception cref="ArgumentOutOfRangeException">There are no slots, or the cap is negative.</exception>
        public ShakeCore(int slots, float maxAmplitude, int seed)
        {
            if (slots < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slots), "At least one slot is needed.");
            }

            if (maxAmplitude < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAmplitude), "The cap cannot be negative.");
            }

            _amplitude = new float[slots];
            _start = new double[slots];
            _end = new double[slots];
            // The cap is rounded down to the grid, so snapping the clamped offset can never pass the cap.
            _maxAmplitude = Mathf.Floor(maxAmplitude / FeedbackConfig.SHAKE_SNAP_UNITS_PER_STEP) * FeedbackConfig.SHAKE_SNAP_UNITS_PER_STEP;
            _random = new System.Random(seed);
        }

        /// <summary>
        /// Starts a shake in a free slot, or in the one that ends first when none is free. Ignored when the
        /// amplitude or the duration is not positive.
        /// </summary>
        /// <param name="amplitude">Largest offset in world units at the start.</param>
        /// <param name="duration">Seconds until the shake is over.</param>
        /// <param name="now">Current time in seconds.</param>
        public void Add(float amplitude, float duration, double now)
        {
            if (amplitude <= 0f || duration <= 0f)
            {
                return;
            }

            var slot = 0;
            for (var i = 0; i < _amplitude.Length; i++)
            {
                if (_amplitude[i] <= 0f || _end[i] <= now)
                {
                    slot = i;
                    break;
                }

                if (_end[i] < _end[slot])
                {
                    slot = i;
                }
            }

            _amplitude[slot] = amplitude;
            _start[slot] = now;
            _end[slot] = now + duration;
        }

        /// <summary>
        /// Computes the camera offset at a time.
        /// </summary>
        /// <param name="now">Current time in seconds.</param>
        /// <returns>The offset in world units, a multiple of 1/16 on each axis, or zero when nothing is running.</returns>
        public Vector2 Evaluate(double now)
        {
            var x = 0f;
            var y = 0f;
            var active = 0;
            for (var i = 0; i < _amplitude.Length; i++)
            {
                if (_amplitude[i] <= 0f)
                {
                    continue;
                }

                if (_end[i] <= now)
                {
                    _amplitude[i] = 0f;
                    continue;
                }

                active++;
                var remaining = (float)((_end[i] - now) / (_end[i] - _start[i]));
                var size = _amplitude[i] * Mathf.Clamp01(remaining);
                x += size * ((float)_random.NextDouble() * 2f - 1f);
                y += size * ((float)_random.NextDouble() * 2f - 1f);
            }

            if (active == 0)
            {
                return Vector2.zero;
            }

            return new Vector2(Snap(Mathf.Clamp(x, -_maxAmplitude, _maxAmplitude)), Snap(Mathf.Clamp(y, -_maxAmplitude, _maxAmplitude)));
        }

        /// <summary>
        /// Stops every shake.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_amplitude, 0, _amplitude.Length);
        }

        /// <summary>
        /// Rounds a value to the nearest multiple of 1/16 world unit. Never returns negative zero.
        /// </summary>
        /// <param name="value">A length in world units.</param>
        /// <returns>The snapped length.</returns>
        public static float Snap(float value)
        {
            const float STEP = FeedbackConfig.SHAKE_SNAP_UNITS_PER_STEP;
            return Mathf.Round(value / STEP) * STEP + 0f;
        }

        /// <summary>
        /// Counts the slots whose shake has not ended.
        /// </summary>
        /// <param name="now">Current time in seconds.</param>
        /// <returns>The number of shakes still running at that time.</returns>
        public int CountActive(double now)
        {
            var count = 0;
            for (var i = 0; i < _amplitude.Length; i++)
            {
                if (_amplitude[i] > 0f && _end[i] > now)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
