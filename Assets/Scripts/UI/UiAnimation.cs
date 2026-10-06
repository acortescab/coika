namespace Coika.UI
{
    /// <summary>
    /// Timings and sizes of the HUD and Game Over animations (issue #37). All of them run on unscaled time and are
    /// shortened by a motion scale when reduced motion is on.
    /// </summary>
    public static class UiAnimation
    {
        /// <summary>Seconds the score takes to roll to a new value (GDD §8.1).</summary>
        public const float COUNT_UP_SECONDS = 0.3f;

        /// <summary>Seconds of the combo label punch.</summary>
        public const float COMBO_PUNCH_SECONDS = 0.25f;

        /// <summary>Peak scale of the combo label punch.</summary>
        public const float COMBO_PUNCH_SCALE = 1.35f;

        /// <summary>Seconds of the next-piece pop.</summary>
        public const float NEXT_POP_SECONDS = 0.2f;

        /// <summary>Peak scale of the next-piece pop.</summary>
        public const float NEXT_POP_SCALE = 1.25f;

        /// <summary>Seconds after the Game Over view appears before Retry is accepted (avoids accidental taps).</summary>
        public const float RETRY_LOCK_SECONDS = 0.5f;

        /// <summary>Factor applied to every duration when reduced motion is on.</summary>
        public const float REDUCE_MOTION_SCALE = 0.5f;

        /// <summary>
        /// Gives the duration factor for the reduced-motion setting.
        /// </summary>
        /// <param name="reduceMotion">True when reduced motion is on.</param>
        /// <returns>1 normally, <see cref="REDUCE_MOTION_SCALE"/> when reduced motion is on.</returns>
        public static float MotionScale(bool reduceMotion)
        {
            return reduceMotion ? REDUCE_MOTION_SCALE : 1f;
        }
    }
}
