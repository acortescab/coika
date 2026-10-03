using System;
using System.Collections.Generic;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the <see cref="Piece"/> component (issue #4, scope 2): initialization from a tier, the reset of a
    /// reused piece, holding and releasing, and the settled check. The collision events need physics and are
    /// covered in PlayMode.
    /// </summary>
    public class PieceTests
    {
        private const float TOLERANCE = 0.0001f;
        private const string TiersFolder = "Assets/Data/Tiers";

        private readonly List<UnityEngine.Object> _created = new();
        private GameObject _pieceObject;
        private Piece _piece;
        private GameConfig _config;

        /// <summary>
        /// Creates a piece object like the prefab (sprite renderer, body, circle collider and Piece) and a config
        /// with the default settled velocity of 0.2.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, null);
            _pieceObject = new GameObject("Piece", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
            _piece = _pieceObject.GetComponent<Piece>();
        }

        /// <summary>
        /// Destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
                UnityEngine.Object.DestroyImmediate(created);

            _created.Clear();
            UnityEngine.Object.DestroyImmediate(_pieceObject);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// For each of the 11 shipped tiers the collider radius is half the diameter and the sprite is as wide as
        /// the diameter.
        /// </summary>
        [Test]
        public void Initialize_WithEveryShippedTier_MatchesColliderRadiusAndSpriteWidthToTheDiameter()
        {
            var guids = AssetDatabase.FindAssets("t:TierDefinition", new[] { TiersFolder });
            Assert.AreEqual(ThemeDefinition.TIER_COUNT, guids.Length, "The 11 tiers must exist.");

            foreach (var guid in guids)
            {
                var tier = AssetDatabase.LoadAssetAtPath<TierDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(tier.Sprite.AssetGUID));
                Assert.IsNotNull(sprite, $"{tier.name} has no sprite. Run Coika/Setup Tier Data.");

                _piece.Initialize(tier, sprite, _config);

                Assert.AreEqual(tier.DiameterUnits * 0.5f, _piece.Collider.radius, TOLERANCE, tier.name + " collider radius");
                Assert.AreEqual(tier.DiameterUnits, sprite.bounds.size.x, TOLERANCE, tier.name + " sprite width");
                Assert.AreSame(sprite, _pieceObject.GetComponent<SpriteRenderer>().sprite, tier.name + " sprite");
                Assert.AreSame(tier, _piece.Tier, tier.name);
            }
        }

        /// <summary>
        /// The mass is the area of the circle, so a bigger tier is heavier by the square of its radius.
        /// </summary>
        [Test]
        public void Initialize_WithBiggerTier_GivesMassFromAreaScalingWithRadiusSquared()
        {
            var small = CreateTier(1f);
            var big = CreateTier(6f);
            var sprite = CreateSprite(16);

            _piece.Initialize(small, sprite, _config);
            var smallMass = _piece.Rigidbody.mass;
            _piece.Initialize(big, sprite, _config);
            var bigMass = _piece.Rigidbody.mass;

            Assert.AreEqual(Mathf.PI * 0.5f * 0.5f, smallMass, TOLERANCE, "Diameter 1");
            Assert.AreEqual(Mathf.PI * 3f * 3f, bigMass, TOLERANCE, "Diameter 6");
            Assert.AreEqual(36f, bigMass / smallMass, 0.001f, "6 times the diameter is 36 times the mass");
        }

        /// <summary>
        /// Initializing a piece that was used before puts it back to a clean state: scale, rotation, velocity,
        /// merged and held flags, dynamic body, piece layer and collider on.
        /// </summary>
        [Test]
        public void Initialize_OnAUsedPiece_ResetsItsState()
        {
            var tier = CreateTier(1f);
            var sprite = CreateSprite(16);
            _piece.Initialize(tier, sprite, _config);
            _piece.MarkMerged();
            _piece.SetHeld(true);
            _pieceObject.transform.localScale = new Vector3(3f, 3f, 1f);
            _pieceObject.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

            _piece.Initialize(tier, sprite, _config);

            Assert.IsFalse(_piece.Merged, "Merged");
            Assert.IsFalse(_piece.IsHeld, "Held");
            Assert.AreEqual(Vector3.one, _pieceObject.transform.localScale, "Scale");
            Assert.AreEqual(Quaternion.identity, _pieceObject.transform.rotation, "Rotation");
            Assert.AreEqual(Vector2.zero, _piece.Rigidbody.linearVelocity, "Velocity");
            Assert.AreEqual(0f, _piece.Rigidbody.angularVelocity, TOLERANCE, "Angular velocity");
            Assert.AreEqual(RigidbodyType2D.Dynamic, _piece.Rigidbody.bodyType, "Body type");
            Assert.AreEqual(LayerMask.NameToLayer(Piece.LAYER_NAME), _pieceObject.layer, "Layer");
            Assert.IsTrue(_piece.Collider.enabled, "Collider");
        }

        /// <summary>
        /// A held piece is kinematic, on the held layer and has no collider; releasing it undoes all of that.
        /// </summary>
        [Test]
        public void SetHeld_WhenToggled_SwitchesBodyLayerAndCollider()
        {
            _piece.Initialize(CreateTier(1f), CreateSprite(16), _config);

            _piece.SetHeld(true);

            Assert.IsTrue(_piece.IsHeld);
            Assert.AreEqual(RigidbodyType2D.Kinematic, _piece.Rigidbody.bodyType);
            Assert.AreEqual(LayerMask.NameToLayer(Piece.HELD_LAYER_NAME), _pieceObject.layer);
            Assert.IsFalse(_piece.Collider.enabled);

            _piece.SetHeld(false);

            Assert.IsFalse(_piece.IsHeld);
            Assert.AreEqual(RigidbodyType2D.Dynamic, _piece.Rigidbody.bodyType);
            Assert.AreEqual(LayerMask.NameToLayer(Piece.LAYER_NAME), _pieceObject.layer);
            Assert.IsTrue(_piece.Collider.enabled);
        }

        /// <summary>
        /// Two different pieces of the same tier that are free and not merged can merge, from either side.
        /// </summary>
        [Test]
        public void CanMergeWith_TwoFreePiecesOfTheSameTier_IsTrueFromBothSides()
        {
            var tier = CreateTier(1f);
            var other = CreateInitializedPiece(tier);
            _piece.Initialize(tier, CreateSprite(16), _config);

            Assert.IsTrue(_piece.CanMergeWith(other), "This piece asks");
            Assert.IsTrue(other.CanMergeWith(_piece), "The other piece asks");
        }

        /// <summary>
        /// A missing piece, the piece itself, a piece of another tier and a piece without tier cannot merge.
        /// </summary>
        [Test]
        public void CanMergeWith_NullSelfOtherTierOrNoTier_IsFalse()
        {
            var tier = CreateTier(1f);
            _piece.Initialize(tier, CreateSprite(16), _config);
            var differentTier = CreateInitializedPiece(CreateTier(1.5f));
            var uninitialized = CreateUninitializedPiece();

            Assert.IsFalse(_piece.CanMergeWith(null), "Null");
            Assert.IsFalse(_piece.CanMergeWith(_piece), "The same piece");
            Assert.IsFalse(_piece.CanMergeWith(differentTier), "Another tier");
            Assert.IsFalse(uninitialized.CanMergeWith(uninitialized), "No tier, same piece");
            Assert.IsFalse(uninitialized.CanMergeWith(_piece), "This piece has no tier");
            Assert.IsFalse(_piece.CanMergeWith(uninitialized), "The other piece has no tier");
        }

        /// <summary>
        /// A piece that was merged or is held cannot merge, and neither can its partner, whichever asks.
        /// </summary>
        [Test]
        public void CanMergeWith_MergedOrHeldPiece_IsFalseFromBothSides()
        {
            var tier = CreateTier(1f);
            var other = CreateInitializedPiece(tier);
            _piece.Initialize(tier, CreateSprite(16), _config);

            other.MarkMerged();
            Assert.IsFalse(_piece.CanMergeWith(other), "The other piece is merged");
            Assert.IsFalse(other.CanMergeWith(_piece), "This piece asks a merged piece");

            other.Initialize(tier, CreateSprite(16), _config);
            other.SetHeld(true);
            Assert.IsFalse(_piece.CanMergeWith(other), "The other piece is held");
            Assert.IsFalse(other.CanMergeWith(_piece), "A held piece asks");

            other.SetHeld(false);
            _piece.MarkMerged();
            Assert.IsFalse(_piece.CanMergeWith(other), "This piece is merged");
            Assert.IsFalse(other.CanMergeWith(_piece), "The partner is merged");
        }

        /// <summary>
        /// A piece is settled when it moves slower than the settled velocity of the config (0.2).
        /// </summary>
        [Test]
        public void IsSettled_AroundTheSettledVelocity_IsTrueOnlyBelowIt()
        {
            _piece.Initialize(CreateTier(1f), CreateSprite(16), _config);

            _piece.Rigidbody.linearVelocity = new Vector2(0.1f, 0f);
            Assert.IsTrue(_piece.IsSettled, "Slower than the threshold");

            _piece.Rigidbody.linearVelocity = new Vector2(0.3f, 0f);
            Assert.IsFalse(_piece.IsSettled, "Faster than the threshold");
        }

        /// <summary>
        /// The spawn time is the time of the initialization, and a new initialization updates it.
        /// </summary>
        [Test]
        public void Initialize_Always_RecordsTheSpawnTime()
        {
            _piece.Initialize(CreateTier(1f), CreateSprite(16), _config);

            Assert.AreEqual(Time.time, _piece.SpawnTime, TOLERANCE);
        }

        /// <summary>
        /// A missing tier, sprite or config is a programming error and throws.
        /// </summary>
        [Test]
        public void Initialize_WithNullArguments_Throws()
        {
            var tier = CreateTier(1f);
            var sprite = CreateSprite(16);

            Assert.Throws<ArgumentNullException>(() => _piece.Initialize(null, sprite, _config), "tier");
            Assert.Throws<ArgumentNullException>(() => _piece.Initialize(tier, null, _config), "sprite");
            Assert.Throws<ArgumentNullException>(() => _piece.Initialize(tier, sprite, null), "config");
        }

        /// <summary>
        /// Creates a second piece object like the prefab, without calling <see cref="Piece.Initialize"/> on it.
        /// </summary>
        /// <returns>The piece. The test destroys it.</returns>
        private Piece CreateUninitializedPiece()
        {
            var pieceObject = new GameObject("OtherPiece", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
            _created.Add(pieceObject);
            return pieceObject.GetComponent<Piece>();
        }

        /// <summary>
        /// Creates a second piece object and initializes it for a tier.
        /// </summary>
        /// <param name="tier">The tier of the piece.</param>
        /// <returns>The piece. The test destroys it.</returns>
        private Piece CreateInitializedPiece(TierDefinition tier)
        {
            var piece = CreateUninitializedPiece();
            piece.Initialize(tier, CreateSprite(16), _config);
            return piece;
        }

        /// <summary>
        /// Creates an in-memory tier with the given diameter by writing its serialized fields.
        /// </summary>
        /// <param name="diameterUnits">Diameter of the tier in world units.</param>
        /// <returns>The tier. The test destroys it.</returns>
        private TierDefinition CreateTier(float diameterUnits)
        {
            var tier = ScriptableObject.CreateInstance<TierDefinition>();
            _created.Add(tier);

            var serializedTier = new SerializedObject(tier);
            serializedTier.FindProperty("_diameterUnits").floatValue = diameterUnits;
            serializedTier.ApplyModifiedPropertiesWithoutUndo();
            return tier;
        }

        /// <summary>
        /// Creates an in-memory square sprite at 16 pixels per unit.
        /// </summary>
        /// <param name="pixels">Side of the sprite in pixels.</param>
        /// <returns>The sprite. The test destroys it and its texture.</returns>
        private Sprite CreateSprite(int pixels)
        {
            var texture = new Texture2D(pixels, pixels);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, pixels, pixels), new Vector2(0.5f, 0.5f), TierDefinition.PixelsPerUnit);
            _created.Add(texture);
            _created.Add(sprite);
            return sprite;
        }
    }
}
