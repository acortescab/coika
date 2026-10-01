using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Drops the largest piece (diameter 6) into a jar built by <see cref="JarBuilder"/> and checks that the
    /// walls and the floor contain it (issue #3, scope 1).
    /// </summary>
    public class JarPhysicsTests
    {
        private const float PIECE_RADIUS = 3f; // Largest tier: diameter 6
        private const float SETTLE_SECONDS = 3f;
        private const float POSITION_TOLERANCE = 0.1f;

        private readonly List<Object> _created = new();

        /// <summary>
        /// Destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
                Object.Destroy(created);

            _created.Clear();
        }

        /// <summary>
        /// A piece dropped straight down from the Drop Line lands on the floor, comes to rest and stays inside the
        /// interior.
        /// </summary>
        [UnityTest]
        public IEnumerator DropLargestPiece_FromDropLine_LandsOnFloorAndRestsInsideInterior()
        {
            yield return DropAndCheck(Vector2.zero, true);
        }

        /// <summary>
        /// A piece thrown hard sideways at the right wall stays inside the interior at every physics step.
        /// </summary>
        [UnityTest]
        public IEnumerator DropLargestPiece_ThrownAtRightWall_StaysInsideInterior()
        {
            yield return DropAndCheck(new Vector2(40f, 0f), false);
        }

        /// <summary>
        /// A piece thrown hard sideways at the left wall stays inside the interior at every physics step.
        /// </summary>
        [UnityTest]
        public IEnumerator DropLargestPiece_ThrownAtLeftWall_StaysInsideInterior()
        {
            yield return DropAndCheck(new Vector2(-40f, 0f), false);
        }

        /// <summary>
        /// Builds a jar and drops a piece with continuous collision detection from the Drop Line with the given
        /// velocity. It checks at every physics step that the piece stays inside the interior bounds, and at the
        /// end that it is on the floor. A thrown piece keeps rolling for a long time, because Box2D has no rolling
        /// resistance, so rest is only required when the caller asks for it.
        /// </summary>
        /// <param name="initialVelocity">Velocity of the piece when it is released.</param>
        /// <param name="expectRest">Whether the piece must be at rest at the end.</param>
        private IEnumerator DropAndCheck(Vector2 initialVelocity, bool expectRest)
        {
            var material = new PhysicsMaterial2D("TestWall") { friction = 0.4f, bounciness = 0f };
            var config = CreateConfig(new Vector2(10f, 12.5f), 1.5f, material);
            _created.Add(material);
            _created.Add(config);

            var jarObject = new GameObject("Jar");
            _created.Add(jarObject);
            var jar = jarObject.AddComponent<Jar>();
            JarBuilder.Build(jar, config);

            var pieceObject = new GameObject("TestPiece") { layer = LayerMask.NameToLayer("Piece") };
            _created.Add(pieceObject);
            pieceObject.transform.position = new Vector3(0f, jar.DropLineY, 0f);
            var body = pieceObject.AddComponent<Rigidbody2D>();
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            pieceObject.AddComponent<CircleCollider2D>().radius = PIECE_RADIUS;
            body.linearVelocity = initialVelocity;

            var elapsed = 0f;
            while (elapsed < SETTLE_SECONDS)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                AssertInsideInterior(jar, pieceObject.transform.position);
            }

            Assert.AreEqual(jar.FloorY + PIECE_RADIUS, pieceObject.transform.position.y, POSITION_TOLERANCE, "The piece should be on the floor.");
            if (expectRest)
                Assert.Less(body.linearVelocity.magnitude, 0.2f, "The piece should be at rest.");
        }

        /// <summary>
        /// Checks that a piece of the largest size centred at the given position is inside the interior bounds of
        /// the jar, within a small tolerance.
        /// </summary>
        /// <param name="jar">The jar that holds the piece.</param>
        /// <param name="position">Centre of the piece in world units.</param>
        private static void AssertInsideInterior(Jar jar, Vector3 position)
        {
            Assert.GreaterOrEqual(position.x - PIECE_RADIUS, jar.InteriorMin.x - POSITION_TOLERANCE, "The piece left through the left wall.");
            Assert.LessOrEqual(position.x + PIECE_RADIUS, jar.InteriorMax.x + POSITION_TOLERANCE, "The piece left through the right wall.");
            Assert.GreaterOrEqual(position.y - PIECE_RADIUS, jar.FloorY - POSITION_TOLERANCE, "The piece fell through the floor.");
        }

        /// <summary>
        /// Creates an in-memory GameConfig with the given jar values by setting its private serialized fields.
        /// </summary>
        /// <param name="jarSize">Interior size of the jar.</param>
        /// <param name="dropLineOffset">Distance from the Danger Line to the Drop Line.</param>
        /// <param name="wallMaterial">Physics material of the walls.</param>
        /// <returns>The config. The test must destroy it.</returns>
        private static GameConfig CreateConfig(Vector2 jarSize, float dropLineOffset, PhysicsMaterial2D wallMaterial)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            SetField(config, "_jarSize", jarSize);
            SetField(config, "_dropLineOffset", dropLineOffset);
            SetField(config, "_wallMaterial", wallMaterial);
            return config;
        }

        /// <summary>
        /// Sets a private instance field by reflection.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the private field.</param>
        /// <param name="value">Value to assign.</param>
        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }
    }
}
