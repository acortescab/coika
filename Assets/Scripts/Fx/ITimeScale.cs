namespace Coika.Fx
{
    /// <summary>
    /// Reads and writes the global time scale. A seam so tests do not touch <c>Time.timeScale</c>.
    /// </summary>
    public interface ITimeScale
    {
        /// <summary>
        /// Gets or sets the time scale.
        /// </summary>
        float Value { get; set; }
    }
}
