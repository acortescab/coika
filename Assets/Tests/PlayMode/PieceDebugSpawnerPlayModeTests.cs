#if UNITY_EDITOR
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
    /// Runs the debug spawner for real (issue #4, scope 4): it finds the config, the Piece prefab and the tiers in
    /// the Editor, builds a factory with the real asset service, and spawns pieces from screen positions. Run with
    /// the Addressables Play Mode Script set to "Use Asset Database".
    /// </summary>
    public class PieceDebugSpawnerPlayModeTests
    {
        private const float POSITION_TOLERANCE = 0.05f;

        private readonly List<Object> _created = new();

        /// <summary>
        /// Destroys everything the test created, so tests stay independent. Destroying the spawner also disposes
        /// its factory.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
                Object.Destroy(created);

            _created.Clear();
        }

        /// <summary>
        /// A spawn from the centre of the screen creates the selected tier under the camera centre; selecting the
        /// largest tier creates that one, kept inside the jar; and a spawn at the screen edge is pulled inside.
        /// </summary>
        [UnityTest]
        public IEnumerator Spawn_WithSelectedTiers_CreatesPiecesAtTheCursorInsideTheJar()
        {
            var jar = CreateJar();
            CreateCamera();
            var spawner = new GameObject("Spawner").AddComponent<PieceDebugSpawner>();
            _created.Add(spawner.gameObject);

            yield return new WaitUntil(() => spawner.IsReady);

            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            var smallest = spawner.Spawn(center);
            Assert.IsNotNull(smallest, "The spawner should create a piece once it is ready.");
            Assert.AreEqual(0, smallest.Tier.Index, "The first tier is selected by default.");
            Assert.AreEqual(0f, smallest.transform.position.x, POSITION_TOLERANCE, "Centred horizontally on the camera.");
            Assert.AreEqual(6.5f, smallest.transform.position.y, POSITION_TOLERANCE, "At the camera centre height.");

            spawner.SelectTier(10);
            var largest = spawner.Spawn(center);
            Assert.AreEqual(10, largest.Tier.Index);
            Assert.AreEqual(3f, largest.Collider.radius, 0.0001f, "The largest tier has diameter 6.");

            spawner.SelectTier(0);
            var atTheEdge = spawner.Spawn(new Vector2(Screen.width, Screen.height * 0.5f));
            var radius = atTheEdge.Collider.radius;
            Assert.LessOrEqual(atTheEdge.transform.position.x + radius, jar.InteriorMax.x + POSITION_TOLERANCE, "Pulled inside the right wall.");

            spawner.ClearPieces();
            Assert.IsFalse(smallest.gameObject.activeSelf, "ClearPieces takes the pieces back to the pool.");
            Assert.IsFalse(largest.gameObject.activeSelf);
            Assert.IsFalse(atTheEdge.gameObject.activeSelf);
        }

        /// <summary>
        /// Builds a jar of the GDD size at the origin.
        /// </summary>
        private Jar CreateJar()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(config);
            SetField(config, "_jarSize", new Vector2(10f, 12.5f));
            SetField(config, "_dropLineOffset", 1.5f);

            var jarObject = new GameObject("Jar");
            _created.Add(jarObject);
            var jar = jarObject.AddComponent<Jar>();
            JarBuilder.Build(jar, config);
            return jar;
        }

        /// <summary>
        /// Creates the Main Camera framing the jar like the Game scene: orthographic, centred at (0, 6.5).
        /// </summary>
        private void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            _created.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 6.5f, -10f);

            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
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
#endif
