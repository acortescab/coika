#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Runs the <see cref="DropController"/> (issue #6) with real physics, the real Piece prefab and the real tiers,
    /// loaded through Addressables, and a scripted input: the piece falls straight down on release, the cooldown
    /// holds the next piece back, the follow never exceeds the maximum speed, and the smallest and the largest
    /// piece stay inside the jar at both walls. Run with the Addressables Play Mode Script set to "Use Asset Database".
    /// </summary>
    public class DropControllerPlayModeTests : InputTestFixture
    {
        private const string ACTIONS_PATH = "Assets/Input/Coika.inputactions";
        private const string PIECE_PREFAB_PATH = "Assets/Prefabs/Piece/Piece.prefab";
        private const string TIER_KEY_FORMAT = "Assets/Data/Tiers/{0}.asset";
        private const float MAX_SPEED = 40f;
        private const float POSITION_TOLERANCE = 0.1f;
        private const float WAIT_LIMIT_SECONDS = 3f;
        private static readonly string[] TierNames = { "Tier_00_Dust", "Tier_01_Pebble", "Tier_02_Asteroid", "Tier_03_Moon", "Tier_04_DwarfPlanet" };

        private readonly List<Object> _created = new();
        private readonly List<TierDefinition> _tiers = new();
        private AssetService _assets;
        private GameConfig _config;
        private Jar _jar;
        private PieceFactory _factory;
        private ScriptedDropInput _input;
        private DropController _controller;
        private SpawnQueue _queue;

        /// <summary>
        /// Disposes the factory, releases the tiers, destroys everything the test created and restores the input system.
        /// </summary>
        public override void TearDown()
        {
            if (_controller != null)
            {
                _controller.Disable();
            }

            _factory?.Dispose();

            foreach (var tier in _tiers)
            {
                _assets.ReleaseAsset(tier);
            }

            _tiers.Clear();

            foreach (var created in _created)
            {
                Object.Destroy(created);
            }

            _created.Clear();
            base.TearDown();
        }

        /// <summary>
        /// The held piece never leaves the jar for any touch X, on the screen edges or far outside the screen, with the
        /// smallest and the largest spawnable piece. The touch is injected into the real reader with virtual devices.
        /// </summary>
        [UnityTest]
        public IEnumerator Touch_AtAnyXIncludingFarOutsideTheScreen_NeverTakesThePieceOutOfTheJar([Values(0, 4)] int tierIndex)
        {
            yield return SetUp(TestFirstTier(tierIndex));
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            var reader = CreateReader();
            _controller.Disable();
            _controller.Initialize(reader, _jar, _factory, _queue, _tiers, _config);
            _controller.Enable();
            var held = _controller.HeldPiece;
            var radius = _tiers[tierIndex].DiameterUnits * 0.5f;
            var middleY = Screen.height * 0.5f;

            BeginTouch(1, new Vector2(Screen.width * 0.5f, middleY), screen: touchscreen);
            yield return null;

            foreach (var screenX in new[] { 0f, Screen.width - 1f, -5000f, 5000f })
            {
                MoveTouch(1, new Vector2(screenX, middleY), screen: touchscreen);
                for (int i = 0; i < 40; i++)
                {
                    yield return new WaitForFixedUpdate();

                    var x = held.Rigidbody.position.x;
                    Assert.GreaterOrEqual(x - radius, _jar.InteriorMin.x - POSITION_TOLERANCE, $"tier {tierIndex} touch x {screenX}, left wall, step {i}");
                    Assert.LessOrEqual(x + radius, _jar.InteriorMax.x + POSITION_TOLERANCE, $"tier {tierIndex} touch x {screenX}, right wall, step {i}");
                }
            }

            EndTouch(1, new Vector2(Screen.width * 0.5f, middleY), screen: touchscreen);
        }

        /// <summary>
        /// A click drops the held piece straight down from where it is; the next piece is held at once but the
        /// input is ignored during the 0.5 s cooldown; and the next drop only happens after the cooldown.
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WhileAiming_DropsStraightDownAndTheCooldownHoldsTheNextPieceBack()
        {
            yield return SetUp(TestFirstTier(0));
            _input.HasPointer = true;
            _input.PointerWorldX = 2f;
            yield return WaitUntilHeldAt(2f);

            var dropped = _controller.HeldPiece;
            var droppedTiers = new List<int>();
            _controller.PieceDropped += droppedTiers.Add;
            var dropStart = Time.time;

            _input.Click();
            yield return null;

            Assert.IsFalse(dropped.IsHeld, "The piece is released.");
            Assert.AreEqual(1, droppedTiers.Count, "PieceDropped was raised once.");
            Assert.AreEqual(DropState.Dropping, _controller.State);
            Assert.AreNotSame(dropped, _controller.HeldPiece, "The next piece is held at once.");

            var startX = dropped.Rigidbody.position.x;
            var startY = dropped.Rigidbody.position.y;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(startX, dropped.Rigidbody.position.x, POSITION_TOLERANCE, "It fell straight down.");
            Assert.Less(dropped.Rigidbody.position.y, startY - 0.2f, "It is falling.");

            _input.PointerWorldX = -3f;
            _input.Click();
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(1, droppedTiers.Count, "Input during the cooldown does nothing.");
            Assert.AreEqual(2f, _controller.HeldX, POSITION_TOLERANCE, "The next piece does not follow during the cooldown.");

            yield return WaitUntilState(DropState.Aiming);
            Assert.GreaterOrEqual(Time.time - dropStart, 0.45f, "The cooldown lasts about 0.5 s.");

            _input.Click();
            yield return new WaitUntil(() => droppedTiers.Count == 2);
            Assert.AreEqual(2, droppedTiers.Count, "After the cooldown it drops again, once the piece reaches the release point.");
        }

        /// <summary>
        /// A pointer that jumps from one side of the screen to the other in a frame never moves the piece faster
        /// than 40 units per second, measured on the physics body at every physics step.
        /// </summary>
        [UnityTest]
        public IEnumerator Follow_WithAPointerJumpingAcrossTheScreen_NeverExceedsTheMaximumSpeed()
        {
            yield return SetUp(TestFirstTier(0));
            _input.HasPointer = true;
            var held = _controller.HeldPiece;
            var previous = held.Rigidbody.position.x;
            var worstSpeed = 0f;

            for (int i = 0; i < 60; i++)
            {
                _input.PointerWorldX = (i / 3) % 2 == 0 ? 100f : -100f;
                yield return new WaitForFixedUpdate();

                var x = held.Rigidbody.position.x;
                worstSpeed = Mathf.Max(worstSpeed, Mathf.Abs(x - previous) / Time.fixedDeltaTime);
                previous = x;
            }

            Assert.LessOrEqual(worstSpeed, MAX_SPEED + 0.5f, "Fastest speed measured, in units per second.");
            Assert.Greater(worstSpeed, 1f, "The piece did move, so the measure means something.");
        }

        /// <summary>
        /// The smallest piece (tier 0) and the largest spawnable one (tier 4) stop at both walls with their whole
        /// body inside the jar.
        /// </summary>
        [UnityTest]
        public IEnumerator Follow_WithTierZeroAndTierFourAgainstBothWalls_StaysInsideTheJar([Values(0, 4)] int tierIndex)
        {
            yield return SetUp(TestFirstTier(tierIndex));
            _input.HasPointer = true;
            var held = _controller.HeldPiece;
            var radius = _tiers[tierIndex].DiameterUnits * 0.5f;
            Assert.AreEqual(tierIndex, held.Tier.Index);

            foreach (var target in new[] { 100f, -100f })
            {
                _input.PointerWorldX = target;
                for (int i = 0; i < 40; i++)
                {
                    yield return new WaitForFixedUpdate();

                    var x = held.Rigidbody.position.x;
                    Assert.GreaterOrEqual(x - radius, _jar.InteriorMin.x - POSITION_TOLERANCE, $"tier {tierIndex} left wall, step {i}");
                    Assert.LessOrEqual(x + radius, _jar.InteriorMax.x + POSITION_TOLERANCE, $"tier {tierIndex} right wall, step {i}");
                }

                var expected = target > 0f ? _jar.InteriorMax.x - radius : _jar.InteriorMin.x + radius;
                Assert.AreEqual(expected, held.Rigidbody.position.x, POSITION_TOLERANCE, $"tier {tierIndex} rests against the wall");
            }
        }

        /// <summary>
        /// A press noted before the component is deactivated, and one made while it is inactive, never drop when
        /// it is activated again: the controller stops listening and forgets what it had noted.
        /// </summary>
        [UnityTest]
        public IEnumerator Click_BeforeOrWhileTheComponentIsDeactivated_DoesNotDropOnReactivation()
        {
            yield return SetUp(TestFirstTier(0));
            var held = _controller.HeldPiece;
            _input.Click();

            _controller.gameObject.SetActive(false);
            _input.Click();
            _controller.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual(0, _queue.GetState().AdvanceCount, "Nothing was dropped.");
            Assert.AreSame(held, _controller.HeldPiece);
            Assert.IsTrue(held.IsHeld);

            _input.Click();
            yield return null;
            Assert.AreEqual(1, _queue.GetState().AdvanceCount, "It listens again once it is active.");
        }

        /// <summary>
        /// Creates a camera and a pointer reader over the real actions, so a test can drive the controller with
        /// virtual devices.
        /// </summary>
        /// <returns>The reader, enabled.</returns>
        private PointerInputReader CreateReader()
        {
            var cameraObject = new GameObject("Camera");
            _created.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;

            var readerObject = new GameObject("Reader");
            _created.Add(readerObject);
            readerObject.SetActive(false);
            var reader = readerObject.AddComponent<PointerInputReader>();
            SetField(reader, "_camera", camera);
            SetField(reader, "_actions", UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(ACTIONS_PATH));
            readerObject.SetActive(true);
            return reader;
        }

        /// <summary>
        /// Settings with a forced opening that makes the first piece the given tier.
        /// </summary>
        /// <param name="tierIndex">Tier of the first piece.</param>
        private static SpawnSettings TestFirstTier(int tierIndex)
        {
            return new SpawnSettings(new[] { 30f, 28f, 20f, 14f, 8f }, 5, 3, new[] { tierIndex });
        }

        /// <summary>
        /// Loads the tiers, builds the jar, the factory and the controller, and enables it.
        /// </summary>
        /// <param name="settings">Spawn values of the queue.</param>
        private IEnumerator SetUp(SpawnSettings settings)
        {
            _assets = new AssetService();
            var loading = LoadTiersAsync();
            yield return new WaitUntil(() => loading.IsCompleted);
            Assert.IsFalse(loading.IsFaulted, loading.Exception?.ToString());

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(_config);
            SetField(_config, "_jarSize", new Vector2(10f, 12.5f));
            SetField(_config, "_dropLineOffset", 1.5f);

            var jarObject = new GameObject("Jar");
            _created.Add(jarObject);
            _jar = jarObject.AddComponent<Jar>();
            JarBuilder.Build(_jar, _config);

            var container = new GameObject("PieceContainer");
            _created.Add(container);
            _factory = new PieceFactory(_assets, new AssetReference(GetPrefabGuid()), _config, container.transform, 8);
            var prewarm = _factory.PrewarmAsync(_tiers);
            yield return new WaitUntil(() => prewarm.IsCompleted);
            Assert.IsFalse(prewarm.IsFaulted, prewarm.Exception?.ToString());

            _input = new ScriptedDropInput();
            var controllerObject = new GameObject("DropController");
            _created.Add(controllerObject);
            _controller = controllerObject.AddComponent<DropController>();
            _queue = new SpawnQueue(settings, 12345);
            _controller.Initialize(_input, _jar, _factory, _queue, _tiers, _config);
            _controller.Enable();
            yield return new WaitForFixedUpdate();
        }

        /// <summary>
        /// Loads the first five tiers through the asset service.
        /// </summary>
        private async Task LoadTiersAsync()
        {
            foreach (var name in TierNames)
            {
                _tiers.Add(await _assets.LoadAsset<TierDefinition>(string.Format(TIER_KEY_FORMAT, name)));
            }
        }

        /// <summary>
        /// Waits, in physics steps, until the held piece is at the given X, and fails if it takes too long.
        /// </summary>
        /// <param name="x">World X to reach.</param>
        private IEnumerator WaitUntilHeldAt(float x)
        {
            var elapsed = 0f;
            while (Mathf.Abs(_controller.HeldPiece.Rigidbody.position.x - x) > 0.05f)
            {
                Assert.Less(elapsed, WAIT_LIMIT_SECONDS, "The held piece never reached the pointer.");
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
        }

        /// <summary>
        /// Waits until the controller is in the given state, and fails if it takes too long.
        /// </summary>
        /// <param name="state">The state to wait for.</param>
        private IEnumerator WaitUntilState(DropState state)
        {
            var elapsed = 0f;
            while (_controller.State != state)
            {
                Assert.Less(elapsed, WAIT_LIMIT_SECONDS, $"The controller never reached {state}.");
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        /// <summary>
        /// Returns the GUID of the Piece prefab. Only the Editor can look it up, and these tests only run there.
        /// </summary>
        private static string GetPrefabGuid()
        {
            return UnityEditor.AssetDatabase.AssetPathToGUID(PIECE_PREFAB_PATH);
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
