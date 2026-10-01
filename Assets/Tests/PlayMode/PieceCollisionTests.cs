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
    /// Checks that pieces report their contacts through <see cref="Piece.Collided"/> (issue #4, scope 2). The
    /// event must keep firing while two pieces rest against each other, because chain merges (issue #7) need it.
    /// </summary>
    public class PieceCollisionTests
    {
        private const float DIAMETER = 1f;

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
        /// Two pieces of the same tier pushed against each other on the jar floor raise <see cref="Piece.Collided"/>
        /// with each other when they touch, and keep raising it while they rest, which only OnCollisionStay2D can do.
        /// </summary>
        [UnityTest]
        public IEnumerator Collided_WithRestingSameTierPieces_FiresWhileTheyStayInContact()
        {
            var config = CreateConfig();
            var jarObject = new GameObject("Jar");
            _created.Add(jarObject);
            var jar = jarObject.AddComponent<Jar>();
            JarBuilder.Build(jar, config);

            var tier = CreateTier(DIAMETER);
            // Start 1.04 apart (a piece is 1 wide) and closing at 1 unit per second: they touch after about 0.04 s.
            var left = CreatePiece(tier, config, new Vector2(-0.52f, jar.FloorY + DIAMETER * 0.5f), new Vector2(0.5f, 0f));
            var right = CreatePiece(tier, config, new Vector2(0.52f, jar.FloorY + DIAMETER * 0.5f), new Vector2(-0.5f, 0f));

            var events = 0;
            var wrongPair = 0;
            left.Collided += (self, other) =>
            {
                events++;
                if (self != left || other != right)
                    wrongPair++;
            };

            yield return new WaitForSeconds(0.15f);
            var eventsAfterTouching = events;

            // Short enough that the resting bodies have not gone to sleep yet, which would stop the contact events.
            yield return new WaitForSeconds(0.2f);

            Assert.Greater(eventsAfterTouching, 0, "The pieces should have touched.");
            Assert.Greater(events, eventsAfterTouching, "Collided must keep firing while the pieces rest in contact.");
            Assert.AreEqual(0, wrongPair, "The event must report this piece and the other piece.");
        }

        /// <summary>
        /// Creates a piece for a tier, places it and gives it a velocity.
        /// </summary>
        /// <param name="tier">Tier of the piece.</param>
        /// <param name="config">Config with the settled velocity.</param>
        /// <param name="position">Starting position in world units.</param>
        /// <param name="velocity">Starting velocity.</param>
        private Piece CreatePiece(TierDefinition tier, GameConfig config, Vector2 position, Vector2 velocity)
        {
            var pieceObject = new GameObject("Piece", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
            _created.Add(pieceObject);
            pieceObject.transform.position = position;

            var piece = pieceObject.GetComponent<Piece>();
            piece.Initialize(tier, CreateSprite(), config);
            piece.Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            piece.Rigidbody.linearVelocity = velocity;
            return piece;
        }

        /// <summary>
        /// Creates an in-memory GameConfig with a jar of the GDD size by setting its private serialized fields.
        /// </summary>
        private GameConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(config);
            SetField(config, "_jarSize", new Vector2(10f, 12.5f));
            SetField(config, "_dropLineOffset", 1.5f);
            return config;
        }

        /// <summary>
        /// Creates an in-memory tier with the given diameter by setting its private serialized field.
        /// </summary>
        /// <param name="diameterUnits">Diameter of the tier in world units.</param>
        private TierDefinition CreateTier(float diameterUnits)
        {
            var tier = ScriptableObject.CreateInstance<TierDefinition>();
            _created.Add(tier);
            SetField(tier, "_diameterUnits", diameterUnits);
            return tier;
        }

        /// <summary>
        /// Creates an in-memory sprite as wide as the test diameter at 16 pixels per unit.
        /// </summary>
        private Sprite CreateSprite()
        {
            var pixels = Mathf.RoundToInt(DIAMETER * TierDefinition.PixelsPerUnit);
            var texture = new Texture2D(pixels, pixels);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), TierDefinition.PixelsPerUnit);
            _created.Add(texture);
            _created.Add(sprite);
            return sprite;
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
