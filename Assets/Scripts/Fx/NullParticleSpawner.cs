using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// Stands in for a particle spawner that the scene does not have, so the director needs no null checks.
    /// </summary>
    public sealed class NullParticleSpawner : IParticleSpawner
    {
        /// <summary>The shared instance; it holds no state.</summary>
        public static readonly NullParticleSpawner Instance = new();

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="kind">Unused.</param>
        /// <param name="position">Unused.</param>
        /// <param name="color">Unused.</param>
        /// <param name="count">Unused.</param>
        public void Burst(FxKind kind, Vector2 position, Color color, int count)
        {
        }
    }
}
