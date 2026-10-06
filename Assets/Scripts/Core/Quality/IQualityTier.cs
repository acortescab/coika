namespace Coika.Core
{
    /// <summary>
    /// The performance tier of the device and what it costs the game to run at it. Read-only: there is no setting
    /// that changes it (issue #39).
    /// </summary>
    public interface IQualityTier
    {
        /// <summary>Gets the tier chosen for this device.</summary>
        QualityLevel Level { get; }

        /// <summary>Gets the factor to apply to particle counts: 1 for full quality, lower on weak devices.</summary>
        float ParticleCountMultiplier { get; }

        /// <summary>Gets a value indicating whether the post-processing Volume may be on.</summary>
        bool PostProcessingEnabled { get; }
    }
}
