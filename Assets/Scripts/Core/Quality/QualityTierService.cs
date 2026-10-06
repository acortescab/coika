namespace Coika.Core
{
    /// <summary>
    /// Decides the tier once from the device figures and derives the effect costs from it.
    /// </summary>
    public sealed class QualityTierService : IQualityTier
    {
        /// <summary>The particle count factor on a <see cref="QualityLevel.Low"/> device.</summary>
        public const float LOW_PARTICLE_FACTOR = 0.5f;

        /// <inheritdoc />
        public QualityLevel Level { get; }

        /// <inheritdoc />
        public float ParticleCountMultiplier => Level == QualityLevel.Low ? LOW_PARTICLE_FACTOR : 1f;

        /// <inheritdoc />
        public bool PostProcessingEnabled => Level == QualityLevel.Normal;

        /// <summary>
        /// Classifies the device.
        /// </summary>
        /// <param name="systemInfo">The device figures to classify.</param>
        public QualityTierService(ISystemInfo systemInfo)
        {
            Level = QualityClassifier.Classify(systemInfo.ProcessorCount, systemInfo.SystemMemoryMb);
        }
    }
}
