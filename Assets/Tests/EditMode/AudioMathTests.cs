using Coika.Core;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests the pure audio formulas of GDD §11 and §9 (issue #29).
    /// </summary>
    public class AudioMathTests
    {
        /// <summary>
        /// Full volume is 0 dB.
        /// </summary>
        [Test]
        public void VolumeToDb_FullVolume_IsZero()
        {
            Assert.AreEqual(0f, AudioMath.VolumeToDb(1f), 0.001f);
        }

        /// <summary>
        /// Half volume is about -6 dB.
        /// </summary>
        [Test]
        public void VolumeToDb_HalfVolume_IsAboutMinusSix()
        {
            Assert.AreEqual(-6.02f, AudioMath.VolumeToDb(0.5f), 0.05f);
        }

        /// <summary>
        /// Zero volume is the silence floor, not minus infinity.
        /// </summary>
        [Test]
        public void VolumeToDb_Zero_IsMinus80()
        {
            Assert.AreEqual(-80f, AudioMath.VolumeToDb(0f));
        }

        /// <summary>
        /// A tiny volume cannot go below the floor, and a volume above 1 is clamped to 0 dB.
        /// </summary>
        [Test]
        public void VolumeToDb_OutOfRange_IsClamped()
        {
            Assert.AreEqual(-80f, AudioMath.VolumeToDb(0.00001f));
            Assert.AreEqual(0f, AudioMath.VolumeToDb(2f), 0.001f);
        }

        /// <summary>
        /// Without a combo the pitch is 1 + 0.06 per tier, for tiers 0 to 10.
        /// </summary>
        [Test]
        public void MergePitch_WithoutCombo_FollowsTheTier()
        {
            for (int tier = 0; tier <= 10; tier++)
            {
                Assert.AreEqual(1f + 0.06f * tier, AudioMath.MergePitch(tier, 0), 0.0001f, $"tier {tier}");
            }
        }

        /// <summary>
        /// Every combo step raises the pitch by one semitone.
        /// </summary>
        [Test]
        public void MergePitch_PerComboStep_RaisesOneSemitone()
        {
            var semitone = (float)System.Math.Pow(2.0, 1.0 / 12.0);
            Assert.AreEqual(1.3f * semitone, AudioMath.MergePitch(5, 1), 0.0001f);
            Assert.AreEqual(1.3f * semitone * semitone, AudioMath.MergePitch(5, 2), 0.0001f);
        }

        /// <summary>
        /// The combo bonus stops at 5 steps, for every tier.
        /// </summary>
        [Test]
        public void MergePitch_AboveTheComboCap_StopsAtFiveSteps()
        {
            for (int tier = 0; tier <= 10; tier++)
            {
                Assert.AreEqual(AudioMath.MergePitch(tier, 5), AudioMath.MergePitch(tier, 9), 0.0001f, $"tier {tier}");
            }
        }

        /// <summary>
        /// Negative inputs count as 0.
        /// </summary>
        [Test]
        public void MergePitch_NegativeInputs_CountAsZero()
        {
            Assert.AreEqual(1f, AudioMath.MergePitch(-3, -2), 0.0001f);
        }
    }
}
