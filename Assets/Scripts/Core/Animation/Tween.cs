using System;

namespace Coika.Core.Animation
{
    /// <summary>
    /// Allocation-free easing maths for visual animations. It is a set of pure functions of a normalized time, so
    /// callers keep their own elapsed time (driven by an injected clock or a delta time) and nothing here allocates,
    /// reads the clock or touches Unity.
    /// </summary>
    public static class Tween
    {
        /// <summary>Overshoot constant that makes <see cref="OutBack"/> exceed its target by about 10 percent.</summary>
        public const float DEFAULT_OVERSHOOT = 1.70158f;

        /// <summary>
        /// Normalizes an elapsed time into the 0 to 1 range.
        /// </summary>
        /// <param name="elapsed">Seconds since the animation started.</param>
        /// <param name="duration">Length of the animation in seconds. Zero or less counts as already finished.</param>
        /// <returns>0 at the start, 1 at or after the end.</returns>
        public static float Progress(float elapsed, float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            var t = elapsed / duration;
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        /// <summary>
        /// Straight line from 0 to 1.
        /// </summary>
        /// <param name="t">Normalized time, from 0 to 1.</param>
        /// <returns>The same value, clamped to 0 to 1.</returns>
        public static float Linear(float t)
        {
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        /// <summary>
        /// Starts fast and decelerates into the target.
        /// </summary>
        /// <param name="t">Normalized time, from 0 to 1.</param>
        /// <returns>The eased value, from 0 to 1.</returns>
        public static float OutQuad(float t)
        {
            var c = Linear(t);
            return 1f - (1f - c) * (1f - c);
        }

        /// <summary>
        /// Rises past the target and settles back on it, which gives the "pop" of a spawn. It starts at 0, ends at
        /// exactly 1 and its peak grows with <paramref name="overshoot"/>.
        /// </summary>
        /// <param name="t">Normalized time, from 0 to 1.</param>
        /// <param name="overshoot">How far past the target it goes; <see cref="DEFAULT_OVERSHOOT"/> gives about 10 percent. Zero or less disables the overshoot.</param>
        /// <returns>The eased value: 0 at the start, above 1 in the middle, 1 at the end.</returns>
        public static float OutBack(float t, float overshoot)
        {
            var c = Linear(t);
            if (overshoot <= 0f)
            {
                return OutQuad(c);
            }

            var u = c - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }

        /// <summary>
        /// Goes from 0 up to a peak and then down to 1 (a "pop"), in two eased segments. The peak is reached at
        /// <paramref name="peakAt"/> of the duration.
        /// </summary>
        /// <param name="t">Normalized time, from 0 to 1.</param>
        /// <param name="peak">Value reached at the peak, usually above 1.</param>
        /// <param name="peakAt">Fraction of the duration at which the peak happens, between 0 and 1 exclusive.</param>
        /// <returns>0 at the start, <paramref name="peak"/> at the peak, 1 at the end.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="peakAt"/> is not between 0 and 1 exclusive.</exception>
        public static float PopThrough(float t, float peak, float peakAt)
        {
            if (peakAt <= 0f || peakAt >= 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(peakAt), "The peak must happen strictly inside the animation.");
            }

            var c = Linear(t);
            if (c < peakAt)
            {
                return peak * OutQuad(c / peakAt);
            }

            return peak + (1f - peak) * OutQuad((c - peakAt) / (1f - peakAt));
        }

        /// <summary>
        /// A single damped wobble: 0 at the start and at the end, 1 at the middle. It scales a squash or a stretch
        /// so it grows and returns to the rest shape.
        /// </summary>
        /// <param name="t">Normalized time, from 0 to 1.</param>
        /// <returns>A value from 0 to 1 that peaks at t = 0.5.</returns>
        public static float Bump(float t)
        {
            var c = Linear(t);
            return 4f * c * (1f - c);
        }
    }
}
