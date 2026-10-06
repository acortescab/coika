namespace Coika.Core
{
    /// <summary>
    /// The performance tier chosen for the device (GDD §14.5). It is picked once at boot and never changes.
    /// </summary>
    public enum QualityLevel
    {
        /// <summary>A device that can run every effect.</summary>
        Normal,

        /// <summary>A weak device: post-processing off and fewer particles.</summary>
        Low,
    }
}
