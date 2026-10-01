using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks how the debug spawner keeps a spawned piece inside the jar (issue #4, scope 4). The GDD jar is 10
    /// units wide with its floor at y = 0.
    /// </summary>
    public class PieceDebugSpawnerTests
    {
        private const float TOLERANCE = 0.0001f;

        private static readonly Vector2 InteriorMin = new(-5f, 0f);
        private static readonly Vector2 InteriorMax = new(5f, 12.5f);

        /// <summary>
        /// A position that already fits is not moved.
        /// </summary>
        [Test]
        public void ClampInsideJar_WithAPositionThatFits_LeavesItUnchanged()
        {
            var clamped = PieceDebugSpawner.ClampInsideJar(new Vector2(1f, 4f), 0.5f, InteriorMin, InteriorMax);

            Assert.AreEqual(1f, clamped.x, TOLERANCE);
            Assert.AreEqual(4f, clamped.y, TOLERANCE);
        }

        /// <summary>
        /// A position past a wall is pulled back so the whole piece is inside, on either side.
        /// </summary>
        [Test]
        public void ClampInsideJar_WithAPositionPastAWall_PullsThePieceBackInside()
        {
            var right = PieceDebugSpawner.ClampInsideJar(new Vector2(9f, 4f), 1f, InteriorMin, InteriorMax);
            var left = PieceDebugSpawner.ClampInsideJar(new Vector2(-9f, 4f), 1f, InteriorMin, InteriorMax);

            Assert.AreEqual(4f, right.x, TOLERANCE, "Right: 5 - radius");
            Assert.AreEqual(-4f, left.x, TOLERANCE, "Left: -5 + radius");
        }

        /// <summary>
        /// A position below the floor is lifted so the piece rests on it.
        /// </summary>
        [Test]
        public void ClampInsideJar_WithAPositionBelowTheFloor_LiftsThePieceOntoIt()
        {
            var clamped = PieceDebugSpawner.ClampInsideJar(new Vector2(0f, -3f), 1.5f, InteriorMin, InteriorMax);

            Assert.AreEqual(1.5f, clamped.y, TOLERANCE);
        }

        /// <summary>
        /// A piece wider than the interior is centred instead of being pushed against a wall.
        /// </summary>
        [Test]
        public void ClampInsideJar_WithAPieceWiderThanTheInterior_CentersIt()
        {
            var clamped = PieceDebugSpawner.ClampInsideJar(new Vector2(4f, 4f), 6f, InteriorMin, InteriorMax);

            Assert.AreEqual(0f, clamped.x, TOLERANCE);
        }
    }
}
