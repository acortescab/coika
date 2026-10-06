using Coika.Core;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the device tier (issue #39): the thresholds of <see cref="QualityClassifier"/> and what
    /// <see cref="QualityTierService"/> derives from the level.
    /// </summary>
    public class QualityTierTests
    {
        /// <summary>
        /// Builds the service over a fake device.
        /// </summary>
        /// <param name="processors">The fake processor count.</param>
        /// <param name="memoryMb">The fake memory in megabytes.</param>
        /// <returns>The service for that device.</returns>
        private static QualityTierService ServiceFor(int processors, int memoryMb)
        {
            return new QualityTierService(new FakeSystemInfo(processors, memoryMb));
        }

        /// <summary>
        /// Fewer than four processors is Low, whatever the memory.
        /// </summary>
        [Test]
        public void Classify_FewProcessors_IsLow()
        {
            Assert.That(QualityClassifier.Classify(2, 8192), Is.EqualTo(QualityLevel.Low));
            Assert.That(QualityClassifier.Classify(3, 8192), Is.EqualTo(QualityLevel.Low));
        }

        /// <summary>
        /// Less than 3000 MB is Low, whatever the processors.
        /// </summary>
        [Test]
        public void Classify_LittleMemory_IsLow()
        {
            Assert.That(QualityClassifier.Classify(8, 2999), Is.EqualTo(QualityLevel.Low));
        }

        /// <summary>
        /// Exactly the thresholds is Normal: the comparisons are strict.
        /// </summary>
        [Test]
        public void Classify_ExactlyTheThresholds_IsNormal()
        {
            Assert.That(QualityClassifier.Classify(4, 3000), Is.EqualTo(QualityLevel.Normal));
        }

        /// <summary>
        /// A strong device is Normal.
        /// </summary>
        [Test]
        public void Classify_StrongDevice_IsNormal()
        {
            Assert.That(QualityClassifier.Classify(8, 8192), Is.EqualTo(QualityLevel.Normal));
        }

        /// <summary>
        /// A Low device turns post-processing off and halves the particles.
        /// </summary>
        [Test]
        public void Service_LowDevice_DisablesPostProcessingAndLowersParticles()
        {
            var tier = ServiceFor(2, 2048);

            Assert.That(tier.Level, Is.EqualTo(QualityLevel.Low));
            Assert.That(tier.PostProcessingEnabled, Is.False);
            Assert.That(tier.ParticleCountMultiplier, Is.EqualTo(QualityTierService.LOW_PARTICLE_FACTOR));
            Assert.That(tier.ParticleCountMultiplier, Is.LessThan(1f));
        }

        /// <summary>
        /// A Normal device keeps post-processing on and the particles at full count.
        /// </summary>
        [Test]
        public void Service_NormalDevice_KeepsPostProcessingAndFullParticles()
        {
            var tier = ServiceFor(8, 6144);

            Assert.That(tier.Level, Is.EqualTo(QualityLevel.Normal));
            Assert.That(tier.PostProcessingEnabled, Is.True);
            Assert.That(tier.ParticleCountMultiplier, Is.EqualTo(1f));
        }
    }
}
