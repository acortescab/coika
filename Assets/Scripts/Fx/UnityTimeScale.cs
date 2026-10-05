using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// The real <c>Time.timeScale</c>.
    /// </summary>
    public sealed class UnityTimeScale : ITimeScale
    {
        /// <summary>
        /// Gets or sets <c>Time.timeScale</c>.
        /// </summary>
        public float Value
        {
            get => Time.timeScale;
            set => Time.timeScale = value;
        }
    }
}
