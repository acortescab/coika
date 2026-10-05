using Coika.Data;
using Coika.Gameplay;
using Coika.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the shipped Piece prefab (issue #4, scope 1). They read the real prefab, so they fail until
    /// <c>Coika/Build Piece Prefab</c> has been run.
    /// </summary>
    public class PiecePrefabTests
    {
        private const float TOLERANCE = 0.0001f;
        private const string GameConfigPath = "Assets/Data/GameConfig/GameConfig.asset";

        private GameObject _prefab;

        /// <summary>
        /// Loads the prefab asset, failing with a hint when it does not exist yet.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PiecePrefabTool.PrefabPath);
            Assert.IsNotNull(_prefab, $"{PiecePrefabTool.PrefabPath} not found. Run Coika/Build Piece Prefab.");
        }

        /// <summary>
        /// The prefab has the body, the circle collider and the Piece component on the root, and the sprite renderer
        /// and the animator on the visual child, never on the root.
        /// </summary>
        [Test]
        public void Prefab_Always_HasTheRequiredComponents()
        {
            var visual = _prefab.transform.Find(PiecePrefabTool.VISUAL_NAME);
            Assert.IsNotNull(visual, "Sprite child");
            Assert.IsNotNull(visual.GetComponent<SpriteRenderer>(), "SpriteRenderer on the child");
            Assert.IsNotNull(visual.GetComponent<PieceAnimator>(), "PieceAnimator on the child");
            Assert.IsNull(_prefab.GetComponent<SpriteRenderer>(), "No SpriteRenderer on the root");
            Assert.IsNotNull(_prefab.GetComponent<Rigidbody2D>(), "Rigidbody2D");
            Assert.IsNotNull(_prefab.GetComponent<CircleCollider2D>(), "CircleCollider2D");
            Assert.IsNotNull(_prefab.GetComponent<Piece>(), "Piece");
        }

        /// <summary>
        /// The body is dynamic with continuous detection, interpolation and the damping of the issue, and its
        /// mass is the area of the collider (pi times the radius squared).
        /// </summary>
        [Test]
        public void Prefab_Always_HasADynamicContinuousInterpolatedBodyWithMassFromArea()
        {
            var body = _prefab.GetComponent<Rigidbody2D>();
            var radius = _prefab.GetComponent<CircleCollider2D>().radius;

            Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
            Assert.AreEqual(CollisionDetectionMode2D.Continuous, body.collisionDetectionMode);
            Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, body.interpolation);
            Assert.AreEqual(0.1f, body.linearDamping, TOLERANCE, "Linear damping");
            Assert.AreEqual(0.3f, body.angularDamping, TOLERANCE, "Angular damping");
            Assert.IsFalse(body.useAutoMass, "The mass is set from the area, not computed from the density.");
            Assert.AreEqual(Mathf.PI * radius * radius, body.mass, TOLERANCE, "Mass = pi * r^2");
        }

        /// <summary>
        /// The prefab is on the Piece layer and its collider uses the piece material of the GameConfig.
        /// </summary>
        [Test]
        public void Prefab_Always_IsOnPieceLayerWithThePieceMaterial()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);

            Assert.AreEqual(LayerMask.NameToLayer(Piece.LAYER_NAME), _prefab.layer);
            Assert.IsNotNull(config.PieceMaterial, "The GameConfig needs a piece material.");
            Assert.AreSame(config.PieceMaterial, _prefab.GetComponent<CircleCollider2D>().sharedMaterial);
        }

        /// <summary>
        /// The prefab is Addressable in the Core-Data group (C-01).
        /// </summary>
        [Test]
        public void Prefab_Always_IsAddressableInCoreData()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(PiecePrefabTool.PrefabPath));

            Assert.IsNotNull(entry, "The Piece prefab must be Addressable.");
            Assert.AreEqual(TierDataSetup.DataGroupName, entry.parentGroup.Name);
        }
    }
}
