using System;
using System.Text.RegularExpressions;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the geometry and the exposed bounds that <see cref="JarBuilder"/> produces from a GameConfig (issue #3, scope 1).
    /// </summary>
    public class JarBuilderTests
    {
        private const float TOLERANCE = 0.0001f;

        private GameObject _jarObject;
        private Jar _jar;
        private PhysicsMaterial2D _wallMaterial;
        private GameConfig _config;

        /// <summary>
        /// Creates a jar object and a config with the GDD jar values (10 x 12.5, Drop Line 1.5 above the Danger Line).
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _wallMaterial = new PhysicsMaterial2D("TestWall");
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, _wallMaterial);
            _jarObject = new GameObject("Jar");
            _jar = _jarObject.AddComponent<Jar>();
        }

        /// <summary>
        /// Destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_jarObject);
            UnityEngine.Object.DestroyImmediate(_config);
            UnityEngine.Object.DestroyImmediate(_wallMaterial);
        }

        /// <summary>
        /// The jar is a floor and two walls only (open top), all on the Wall layer with the config's material.
        /// </summary>
        [Test]
        public void Build_Always_CreatesFloorAndTwoWallsOnWallLayerWithWallMaterial()
        {
            JarBuilder.Build(_jar, _config);

            var colliders = _jarObject.GetComponentsInChildren<BoxCollider2D>();
            var wallLayer = LayerMask.NameToLayer(JarBuilder.WALL_LAYER_NAME);

            Assert.AreEqual(3, colliders.Length, "The jar must have a floor and two walls, and no top.");
            Assert.AreNotEqual(-1, wallLayer, "The Wall layer must exist.");
            foreach (var collider in colliders)
            {
                Assert.AreEqual(wallLayer, collider.gameObject.layer, collider.name);
                Assert.AreSame(_wallMaterial, collider.sharedMaterial, collider.name);
            }
        }

        /// <summary>
        /// The floor surface is at y = 0, the inner faces of the walls are at the interior width, and the walls
        /// reach the clearance above the Drop Line. The floor is as wide as the walls' outer faces.
        /// </summary>
        [Test]
        public void Build_Always_PlacesFloorAndWallsAroundTheInterior()
        {
            JarBuilder.Build(_jar, _config);

            var floor = GetCollider("Floor");
            var left = GetCollider("WallLeft");
            var right = GetCollider("WallRight");

            Assert.AreEqual(0f, floor.transform.localPosition.y + floor.size.y * 0.5f, TOLERANCE, "Floor surface");
            Assert.AreEqual(10f + 2f * JarBuilder.WALL_THICKNESS, floor.size.x, TOLERANCE, "Floor width");
            Assert.AreEqual(-5f, left.transform.localPosition.x + left.size.x * 0.5f, TOLERANCE, "Left wall inner face");
            Assert.AreEqual(5f, right.transform.localPosition.x - right.size.x * 0.5f, TOLERANCE, "Right wall inner face");

            var wallTop = right.transform.localPosition.y + right.size.y * 0.5f;
            Assert.AreEqual(_jar.DropLineY + JarBuilder.WALL_CLEARANCE_ABOVE_DROP_LINE, wallTop, TOLERANCE, "Wall top");
            Assert.AreEqual(-JarBuilder.WALL_THICKNESS, right.transform.localPosition.y - right.size.y * 0.5f, TOLERANCE, "Wall bottom");
        }

        /// <summary>
        /// The bounds and lines follow the GDD: the Danger Line is the floor plus 12.5 and the Drop Line is 1.5 above it,
        /// in world space when the jar is not at the origin.
        /// </summary>
        [Test]
        public void Build_Always_ExposesBoundsAndLinesFromConfig()
        {
            _jarObject.transform.position = new Vector3(2f, 3f, 0f);

            JarBuilder.Build(_jar, _config);

            Assert.AreEqual(3f, _jar.FloorY, TOLERANCE);
            Assert.AreEqual(15.5f, _jar.DangerLineY, TOLERANCE);
            Assert.AreEqual(17f, _jar.DropLineY, TOLERANCE);
            Assert.AreEqual(-3f, _jar.InteriorMin.x, TOLERANCE);
            Assert.AreEqual(3f, _jar.InteriorMin.y, TOLERANCE);
            Assert.AreEqual(7f, _jar.InteriorMax.x, TOLERANCE);
            Assert.AreEqual(15.5f, _jar.InteriorMax.y, TOLERANCE);
        }

        /// <summary>
        /// Building again with other jar values resizes the colliders and the bounds, and does not add colliders.
        /// </summary>
        [Test]
        public void Build_WithNewConfigValues_ResizesWithoutDuplicating()
        {
            JarBuilder.Build(_jar, _config);
            var smallConfig = TestGameConfig.Create(new Vector2(8f, 10f), 2f, _wallMaterial);

            try
            {
                JarBuilder.Build(_jar, smallConfig);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(smallConfig);
            }

            Assert.AreEqual(3, _jarObject.GetComponentsInChildren<BoxCollider2D>().Length);
            Assert.AreEqual(8f + 2f * JarBuilder.WALL_THICKNESS, GetCollider("Floor").size.x, TOLERANCE, "Floor width");
            Assert.AreEqual(4f + JarBuilder.WALL_THICKNESS * 0.5f, GetCollider("WallRight").transform.localPosition.x, TOLERANCE, "Right wall position");
            Assert.AreEqual(10f, _jar.DangerLineY, TOLERANCE);
            Assert.AreEqual(12f, _jar.DropLineY, TOLERANCE);
            Assert.AreEqual(4f, _jar.InteriorMax.x, TOLERANCE);
        }

        /// <summary>
        /// A null config is a programming error and throws.
        /// </summary>
        [Test]
        public void Build_WithNullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => JarBuilder.Build(_jar, null));
        }

        /// <summary>
        /// A null jar is a programming error and throws.
        /// </summary>
        [Test]
        public void Build_WithNullJar_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => JarBuilder.Build(null, _config));
        }

        /// <summary>
        /// A non-positive jar size is reported as an error and nothing is built.
        /// </summary>
        [Test]
        public void Build_WithNonPositiveSize_LogsErrorAndBuildsNothing()
        {
            var badConfig = TestGameConfig.Create(new Vector2(0f, 12.5f), 1.5f, _wallMaterial);
            LogAssert.Expect(LogType.Error, new Regex("Jar size must be positive"));

            try
            {
                JarBuilder.Build(_jar, badConfig);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(badConfig);
            }

            Assert.AreEqual(0, _jarObject.GetComponentsInChildren<BoxCollider2D>().Length);
        }

        /// <summary>
        /// The floor and both walls draw their sprite tiled over exactly the rectangle of their collider, below the
        /// Danger Line.
        /// </summary>
        [Test]
        public void Build_Always_AddsTiledVisualsMatchingTheColliders()
        {
            JarBuilder.Build(_jar, _config);

            foreach (var childName in new[] { "Floor", "WallLeft", "WallRight" })
            {
                var collider = GetCollider(childName);
                var spriteRenderer = collider.GetComponent<SpriteRenderer>();

                Assert.IsNotNull(spriteRenderer, childName);
                Assert.AreEqual(SpriteDrawMode.Tiled, spriteRenderer.drawMode, childName);
                Assert.AreEqual(collider.size.x, spriteRenderer.size.x, TOLERANCE, childName + " width");
                Assert.AreEqual(collider.size.y, spriteRenderer.size.y, TOLERANCE, childName + " height");
                Assert.Less(spriteRenderer.sortingOrder, _jar.DangerLine.GetComponent<SpriteRenderer>().sortingOrder, childName);
            }
        }

        /// <summary>
        /// Building again with other jar values resizes the visuals together with the colliders, and adds no
        /// second sprite renderer.
        /// </summary>
        [Test]
        public void Build_WithNewConfigValues_ResizesVisualsWithColliders()
        {
            JarBuilder.Build(_jar, _config);
            var smallConfig = TestGameConfig.Create(new Vector2(8f, 10f), 2f, _wallMaterial);

            try
            {
                JarBuilder.Build(_jar, smallConfig);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(smallConfig);
            }

            Assert.AreEqual(4, _jarObject.GetComponentsInChildren<SpriteRenderer>().Length, "Floor, two walls and the Danger Line");
            foreach (var childName in new[] { "Floor", "WallLeft", "WallRight" })
            {
                var collider = GetCollider(childName);
                var spriteRenderer = collider.GetComponent<SpriteRenderer>();

                Assert.AreEqual(collider.size.x, spriteRenderer.size.x, TOLERANCE, childName + " width");
                Assert.AreEqual(collider.size.y, spriteRenderer.size.y, TOLERANCE, childName + " height");
            }
        }

        /// <summary>
        /// Regression: Unity resets the size of a tiled sprite renderer when its sprite changes (to the sprite's
        /// own 1 x 1 size here). Building again after the sprites are assigned puts the jar sizes back.
        /// </summary>
        [Test]
        public void Build_AfterSpritesAreAssigned_RestoresVisualSizes()
        {
            JarBuilder.Build(_jar, _config);
            var texture = new Texture2D(16, 16);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);

            try
            {
                foreach (var spriteRenderer in _jarObject.GetComponentsInChildren<SpriteRenderer>())
                    spriteRenderer.sprite = sprite;

                JarBuilder.Build(_jar, _config);

                foreach (var childName in new[] { "Floor", "WallLeft", "WallRight" })
                {
                    var collider = GetCollider(childName);
                    var spriteRenderer = collider.GetComponent<SpriteRenderer>();

                    Assert.AreEqual(collider.size.x, spriteRenderer.size.x, TOLERANCE, childName + " width");
                    Assert.AreEqual(collider.size.y, spriteRenderer.size.y, TOLERANCE, childName + " height");
                }

                Assert.AreEqual(10f, _jar.DangerLine.GetComponent<SpriteRenderer>().size.x, TOLERANCE, "Danger Line width");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Returns the box collider of a named child of the jar.
        /// </summary>
        /// <param name="childName">Name of the floor or wall child.</param>
        private BoxCollider2D GetCollider(string childName)
        {
            return _jarObject.transform.Find(childName).GetComponent<BoxCollider2D>();
        }
    }
}
