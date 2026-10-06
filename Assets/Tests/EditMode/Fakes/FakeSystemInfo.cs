using Coika.Core;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// A device with fixed hardware figures, so tests choose the tier without touching <c>SystemInfo</c>.
    /// </summary>
    public sealed class FakeSystemInfo : ISystemInfo
    {
        /// <inheritdoc />
        public int ProcessorCount { get; }

        /// <inheritdoc />
        public int SystemMemoryMb { get; }

        /// <summary>
        /// Creates a fake device.
        /// </summary>
        /// <param name="processorCount">The logical processor count to report.</param>
        /// <param name="systemMemoryMb">The system memory in megabytes to report.</param>
        public FakeSystemInfo(int processorCount, int systemMemoryMb)
        {
            ProcessorCount = processorCount;
            SystemMemoryMb = systemMemoryMb;
        }
    }
}
