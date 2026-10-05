using System.Collections.Generic;
using Coika.Fx;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Records every particle request instead of drawing it, so a test can assert what a director asked for.
    /// </summary>
    public sealed class FakeParticleSpawner : IParticleSpawner
    {
        /// <summary>Every request, in order.</summary>
        public List<ParticleRequest> Requests { get; } = new();

        /// <inheritdoc />
        public void Burst(FxKind kind, Vector2 position, Color color, int count)
        {
            Requests.Add(new ParticleRequest(kind, position, color, count));
        }

        /// <summary>
        /// Counts the requests of a kind.
        /// </summary>
        /// <param name="kind">The effect.</param>
        /// <returns>The number of requests of that kind.</returns>
        public int Count(FxKind kind)
        {
            var count = 0;
            foreach (var request in Requests)
            {
                if (request.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Returns the first request of a kind.
        /// </summary>
        /// <param name="kind">The effect.</param>
        /// <returns>The first request of that kind, or null.</returns>
        public ParticleRequest First(FxKind kind)
        {
            return Requests.Find(request => request.Kind == kind);
        }

        /// <summary>
        /// Asserts that exactly one request of a kind was made and returns it.
        /// </summary>
        /// <param name="kind">The effect.</param>
        /// <returns>The only request of that kind.</returns>
        public ParticleRequest Single(FxKind kind)
        {
            Assert.AreEqual(1, Count(kind), $"Expected exactly one {kind}.");
            return First(kind);
        }
    }
}
