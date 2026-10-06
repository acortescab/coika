using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// Reads the real device through <see cref="SystemInfo"/>. The only code that does, so the rest runs on a fake.
    /// </summary>
    public sealed class SystemInfoProvider : ISystemInfo
    {
        /// <inheritdoc />
        public int ProcessorCount => SystemInfo.processorCount;

        /// <inheritdoc />
        public int SystemMemoryMb => SystemInfo.systemMemorySize;
    }
}
