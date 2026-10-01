namespace Coika.Gameplay
{
    /// <summary>
    /// Speed of the red pulse of the <see cref="DangerLine"/>. Soft is the accessibility alternative.
    /// </summary>
    public enum DangerLinePulseRate
    {
        /// <summary>Default pulse, 4 cycles per second.</summary>
        Fast,

        /// <summary>Gentler pulse, 2 cycles per second, for players who prefer less flashing.</summary>
        Soft
    }
}
