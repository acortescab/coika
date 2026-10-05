using Coika.Fx;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// One recorded particle request.
    /// </summary>
    public sealed class ParticleRequest
    {
        /// <summary>Creates the record.</summary>
        /// <param name="kind">The effect.</param>
        /// <param name="position">Where it was requested.</param>
        /// <param name="color">The colour.</param>
        /// <param name="count">The requested count.</param>
        public ParticleRequest(FxKind kind, Vector2 position, Color color, int count)
        {
            Kind = kind;
            Position = position;
            Color = color;
            Count = count;
        }

        /// <summary>The effect.</summary>
        public FxKind Kind { get; }

        /// <summary>Where it was requested.</summary>
        public Vector2 Position { get; }

        /// <summary>The colour.</summary>
        public Color Color { get; }

        /// <summary>The requested count.</summary>
        public int Count { get; }
    }
}
