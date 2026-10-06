namespace Coika.Core
{
    /// <summary>
    /// Maps the hardware figures of a device to a <see cref="QualityLevel"/>. Pure, so the thresholds are tested with
    /// plain numbers instead of a faked <c>SystemInfo</c>.
    /// </summary>
    public static class QualityClassifier
    {
        /// <summary>Devices with fewer logical processors than this are Low.</summary>
        public const int MIN_PROCESSOR_COUNT = 4;

        /// <summary>Devices with less system memory than this, in megabytes, are Low.</summary>
        public const int MIN_MEMORY_MB = 3000;

        /// <summary>
        /// Chooses the tier for a device.
        /// </summary>
        /// <param name="processorCount">The number of logical processors.</param>
        /// <param name="systemMemoryMb">The system memory in megabytes.</param>
        /// <returns><see cref="QualityLevel.Low"/> below either threshold, otherwise <see cref="QualityLevel.Normal"/>.</returns>
        public static QualityLevel Classify(int processorCount, int systemMemoryMb)
        {
            return processorCount < MIN_PROCESSOR_COUNT || systemMemoryMb < MIN_MEMORY_MB
                ? QualityLevel.Low
                : QualityLevel.Normal;
        }
    }
}
