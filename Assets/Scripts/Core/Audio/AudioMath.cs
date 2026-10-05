using System;

namespace Coika.Core
{
    /// <summary>
    /// Pure audio formulas of GDD §11 and §9, kept apart from the engine so they can be tested without a scene.
    /// </summary>
    public static class AudioMath
    {
        /// <summary>Volume in dB that stands for silence.</summary>
        public const float MIN_DB = -80f;

        /// <summary>Pitch gain of a merge for every tier.</summary>
        public const float PITCH_PER_TIER = 0.06f;

        /// <summary>Highest combo step that still raises the pitch.</summary>
        public const int MAX_COMBO_STEPS = 5;

        /// <summary>
        /// Maps a 0 to 1 volume setting to decibels with 20·log10(v). 1 gives 0 dB, 0.5 about −6 dB and 0 gives
        /// <see cref="MIN_DB"/>.
        /// </summary>
        /// <param name="volume">Volume from 0 to 1. Values outside the range are clamped.</param>
        /// <returns>Decibels, never below <see cref="MIN_DB"/> and never above 0.</returns>
        public static float VolumeToDb(float volume)
        {
            if (volume <= 0f)
            {
                return MIN_DB;
            }

            var db = 20f * (float)Math.Log10(Math.Min(volume, 1f));
            return Math.Max(db, MIN_DB);
        }

        /// <summary>
        /// Pitch of a merge sound: (1 + 0.06·tier), raised one semitone for every combo step, up to
        /// <see cref="MAX_COMBO_STEPS"/>.
        /// </summary>
        /// <param name="tier">Tier of the piece that was created. Negative values count as 0.</param>
        /// <param name="comboStep">Current combo step. Negative values count as 0.</param>
        /// <returns>The pitch multiplier for <c>AudioSource.pitch</c>.</returns>
        public static float MergePitch(int tier, int comboStep)
        {
            var steps = Math.Min(Math.Max(comboStep, 0), MAX_COMBO_STEPS);
            var basePitch = 1f + PITCH_PER_TIER * Math.Max(tier, 0);
            return basePitch * (float)Math.Pow(2.0, steps / 12.0);
        }
    }
}
