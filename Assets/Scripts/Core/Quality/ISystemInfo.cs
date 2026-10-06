namespace Coika.Core
{
    /// <summary>
    /// The hardware figures the quality tier needs. A seam over <c>UnityEngine.SystemInfo</c> so tests can fake a device.
    /// </summary>
    public interface ISystemInfo
    {
        /// <summary>Gets the number of logical processors.</summary>
        int ProcessorCount { get; }

        /// <summary>Gets the system memory in megabytes.</summary>
        int SystemMemoryMb { get; }
    }
}
