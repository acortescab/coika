using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// Emits the pooled visual effects. The seam between the code that decides when an effect happens
    /// (<see cref="FxDirector"/>) and the one that draws it, so tests can record the requests.
    /// </summary>
    public interface IParticleSpawner
    {
        /// <summary>
        /// Emits one effect without instantiating or destroying anything.
        /// </summary>
        /// <param name="kind">The effect.</param>
        /// <param name="position">World position of the effect.</param>
        /// <param name="color">Tint of the particles; rings and flashes are always white.</param>
        /// <param name="count">Requested particle count before scaling; ignored for single-ring effects.</param>
        void Burst(FxKind kind, Vector2 position, Color color, int count);
    }
}
