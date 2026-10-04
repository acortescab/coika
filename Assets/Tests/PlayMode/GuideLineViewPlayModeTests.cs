#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Runs the <see cref="GuideLineView"/> (issue #28) with real physics, the real Piece prefab, the real tiers and
    /// the real GuideLine prefab: the ghost sits where a real straight drop first touches something, the guide
    /// follows the visibility rules and the setting, stays inside the jar, and allocates nothing. Run with the
    /// Addressables Play Mode Script set to "Use Asset Database".
    /// </summary>
    public class GuideLineViewPlayModeTests
    {
        private const string PIECE_PREFAB_PATH = "Assets/Prefabs/Piece/Piece.prefab";
        private const string GUIDE_PREFAB_PATH = "Assets/Prefabs/Fx/GuideLine.prefab";
        private const string TIER_KEY_FORMAT = "Assets/Data/Tiers/{0}.asset";
        private const float LANDING_TOLERANCE = 0.05f;
        private const float WAIT_LIMIT_SECONDS = 5f;
        private const float SUBSTEP_SECONDS = 0.001f;
        private const int MAX_SUBSTEPS = 5000;
        private const int SETTLED_STEPS = 15;
        private const float OBSTACLE_REST_SECONDS = 1.5f;
        private static readonly string[] TierNames = { "Tier_00_Dust", "Tier_01_Pebble", "Tier_02_Asteroid", "Tier_03_Moon", "Tier_04_DwarfPlanet" };

        private readonly List<Object> _created = new();
        private readonly List<TierDefinition> _tiers = new();
        private AssetService _assets;
        private GameConfig _config;
        private Jar _jar;
        private PieceFactory _factory;
        private ScriptedDropInput _input;
        private DropController _controller;
        private GuideLineView _view;

        /// <summary>
        /// Disposes the factory, releases the tiers and destroys everything the test created.
        /// </summary>
        [TearDown]
        public void TearDown()
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
        }

        /// <summary>
        /// The ghost sits on the floor where a real piece, dropped straight down from the same X, comes to rest.
        /// </summary>
        [UnityTest]
        public IEnumerator Ghost_OverTheFloor_SitsWhereARealDropComesToRest()
        {
            yield return SetUp(0);
            _input.HasPointer = true;
            _input.PointerWorldX = 2f;
            _input.Press();
            yield return WaitUntilHeldAt(2f);
            yield return new WaitForFixedUpdate();
            _view.Refresh();
            Assert.IsTrue(_view.IsShown, "The guide is visible while aiming.");
            var ghost = (Vector2)_view.Ghost.transform.position;
            var dropped = _controller.HeldPiece;

            _input.Release();
            yield return WaitUntilFalling(dropped);
            yield return WaitUntilSettled(dropped);

            Assert.AreEqual(ghost.x, dropped.Rigidbody.position.x, LANDING_TOLERANCE, "Same X.");
            Assert.AreEqual(ghost.y, dropped.Rigidbody.position.y, LANDING_TOLERANCE, "Same height.");
        }

        /// <summary>
        /// Over a piece that already rests in the jar, the ghost sits where a real piece first touches it.
        /// </summary>
        [UnityTest]
        public IEnumerator Ghost_OverAnotherPiece_SitsWhereARealDropFirstTouchesIt()
        {
            yield return SetUp(0);
            var obstacle = _factory.Create(_tiers[4], new Vector2(-2f, _jar.FloorY + 2f), Vector2.zero);
            yield return new WaitForSeconds(OBSTACLE_REST_SECONDS);

            _input.HasPointer = true;
            _input.PointerWorldX = -2f;
            _input.Press();
            yield return WaitUntilHeldAt(-2f);
            yield return new WaitForFixedUpdate();
            _view.Refresh();
            var ghost = (Vector2)_view.Ghost.transform.position;
            var dropped = _controller.HeldPiece;
            var contact = Vector2.zero;
            var touched = false;
            dropped.Collided += (self, other) =>
            {
                if (!touched && other == obstacle)
                {
                    touched = true;
                    contact = self.Rigidbody.position;
                }
            };

            _input.Release();
            yield return null;

            // Tiny manual steps, so the contact is seen within a hair of where it happens instead of up to one
            // 20 ms step (about 0.3 unit at falling speed) late.
            var previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            try
            {
                var steps = 0;
                while (!touched)
                {
                    Assert.Less(steps++, MAX_SUBSTEPS, "The dropped piece never touched the other one.");
                    Physics2D.Simulate(SUBSTEP_SECONDS);
                }
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
            }

            Assert.AreEqual(ghost.y, contact.y, LANDING_TOLERANCE, "The ghost is where the first touch happens.");
            Assert.AreEqual(ghost.x, contact.x, LANDING_TOLERANCE);
        }

        /// <summary>
        /// The setting hides the guide at once and shows it again at once.
        /// </summary>
        [UnityTest]
        public IEnumerator SetSettingOn_TurnedOffAndOn_HidesAndShowsTheGuideImmediately()
        {
            yield return SetUp(0);
            _input.Press();
            yield return null;
            _view.Refresh();
            Assert.IsTrue(_view.IsShown);

            _view.SetSettingOn(false);
            Assert.IsFalse(_view.IsShown);
            Assert.IsFalse(_view.Ghost.enabled);
            Assert.IsFalse(_view.Line.enabled);

            _view.SetSettingOn(true);
            Assert.IsTrue(_view.IsShown);
            Assert.IsTrue(_view.Ghost.enabled);
        }

        /// <summary>
        /// The guide is drawn only while the player presses: not before, and not at once after the drop, when the
        /// next piece is held but nobody is pressing.
        /// </summary>
        [UnityTest]
        public IEnumerator Refresh_BeforePressingWhilePressingAndAfterTheDrop_ShowsTheGuideOnlyWhilePressing()
        {
            yield return SetUp(0);
            _view.Refresh();
            Assert.IsFalse(_view.IsShown, "Hidden while the piece is only held.");

            _input.Press();
            yield return null;
            _view.Refresh();
            Assert.IsTrue(_view.IsShown, "Visible while pressing.");

            _input.Release();
            yield return null;
            Assert.AreEqual(DropState.Dropping, _controller.State);
            _view.Refresh();
            Assert.IsFalse(_view.IsShown, "Hidden as soon as the piece is dropped.");
            Assert.IsFalse(_view.Ghost.enabled);
            Assert.IsFalse(_view.Line.enabled);
        }

        /// <summary>
        /// Pressing soon after a drop, while the cooldown still runs, and holding on: the guide appears as soon as the
        /// piece is controllable, but letting go drops it (the release decides, not when the press began).
        /// </summary>
        [UnityTest]
        public IEnumerator Refresh_WhenPressedDuringTheCooldownAndHeld_ShowsTheGuideOnceAimingResumesAndDropsOnRelease()
        {
            yield return SetUp(0);
            _input.Click();
            yield return null;
            Assert.AreEqual(DropState.Dropping, _controller.State);

            _input.HasPointer = true;
            _input.PointerWorldX = 3f;
            _input.Press();
            yield return null;
            _view.Refresh();
            Assert.IsFalse(_view.IsShown, "Hidden while the cooldown runs.");

            var elapsed = 0f;
            while (_controller.State != DropState.Aiming)
            {
                Assert.Less(elapsed, WAIT_LIMIT_SECONDS, "The cooldown never ended.");
                yield return null;
                elapsed += Time.deltaTime;
            }

            yield return null;
            _view.Refresh();
            Assert.IsTrue(_view.IsShown, "Visible once the piece is controllable and the press is held.");
            Assert.Less(_controller.HeldPiece.transform.position.x, 2.9f, "The piece slides to the finger instead of jumping.");
            yield return WaitUntilHeldAt(3f);
            yield return new WaitForFixedUpdate();
            _view.Refresh();
            Assert.IsTrue(_view.IsShown, "Still visible while the piece slides.");
            Assert.AreEqual(3f, _controller.HeldPiece.transform.position.x, LANDING_TOLERANCE, "The piece reached the finger.");
            Assert.AreEqual(3f, _view.Ghost.transform.position.x, LANDING_TOLERANCE, "The ghost is under the finger.");

            var held = _controller.HeldPiece;
            _input.Release();
            yield return null;
            Assert.IsFalse(held.IsHeld, "A press that began during the cooldown still drops on release.");
            _view.Refresh();
            Assert.IsFalse(_view.IsShown);
        }

        /// <summary>
        /// A cancelled press hides the guide and drops nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator Refresh_AfterACancelledPress_HidesTheGuide()
        {
            yield return SetUp(0);
            _input.Press();
            yield return null;
            _view.Refresh();
            Assert.IsTrue(_view.IsShown);

            _input.Cancel();
            yield return null;
            _view.Refresh();

            Assert.IsFalse(_view.IsShown);
        }

        /// <summary>
        /// The guide is never visible while the controller is disabled, as on pause and game over.
        /// </summary>
        [UnityTest]
        public IEnumerator Refresh_WhenTheControllerIsDisabled_HidesTheGuide()
        {
            yield return SetUp(0);
            _input.Press();
            yield return null;
            _view.Refresh();
            Assert.IsTrue(_view.IsShown);

            _controller.Disable();
            _view.Refresh();

            Assert.IsFalse(_view.IsShown);
            Assert.IsFalse(_view.Ghost.enabled);
            Assert.IsFalse(_view.Line.enabled);
        }

        /// <summary>
        /// At both walls the ghost and the line stay inside the jar, for the smallest and the largest piece.
        /// </summary>
        [UnityTest]
        public IEnumerator Ghost_AtBothWalls_StaysInsideTheJar([Values(0, 4)] int tierIndex)
        {
            yield return SetUp(tierIndex);
            var radius = _tiers[tierIndex].DiameterUnits * 0.5f;
            _input.HasPointer = true;
            _input.Press();

            foreach (var x in new[] { -100f, 100f })
            {
                _input.PointerWorldX = x;
                for (var i = 0; i < 60; i++)
                {
                    yield return new WaitForFixedUpdate();
                }

                _view.Refresh();
                var ghost = _view.Ghost.transform.position;
                Assert.GreaterOrEqual(ghost.x - radius, _jar.InteriorMin.x - LANDING_TOLERANCE, $"ghost, left, pointer {x}");
                Assert.LessOrEqual(ghost.x + radius, _jar.InteriorMax.x + LANDING_TOLERANCE, $"ghost, right, pointer {x}");
                Assert.GreaterOrEqual(ghost.y - radius, _jar.FloorY - LANDING_TOLERANCE, $"ghost, floor, pointer {x}");

                if (_view.Line.enabled)
                {
                    var line = _view.Line.transform.position;
                    Assert.GreaterOrEqual(line.x, _jar.InteriorMin.x);
                    Assert.LessOrEqual(line.x, _jar.InteriorMax.x);
                    Assert.LessOrEqual(line.y, _jar.DropLineY);
                    Assert.GreaterOrEqual(line.y - _view.Line.size.y, _jar.FloorY - LANDING_TOLERANCE);
                }
            }
        }

        /// <summary>
        /// With the guide visible and the piece moving, refreshing allocates nothing.
        /// </summary>
        [UnityTest]
        public IEnumerator Refresh_WhileVisibleAndTheXChanges_AllocatesNothing()
        {
            yield return SetUp(0);
            _input.Press();
            yield return null;
            var held = _controller.HeldPiece;
            var left = new Vector3(-1f, held.transform.position.y, 0f);
            var right = new Vector3(1f, held.transform.position.y, 0f);
            _view.Refresh();
            _view.Refresh();

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    held.transform.position = (i & 1) == 0 ? left : right;
                    _view.Refresh();
                }
            });

            Assert.IsTrue(_view.IsShown);
            Assert.That(allocations, Is.LessThanOrEqualTo(AllocationMeter.TOLERANCE_COUNT));
        }

        /// <summary>
        /// Loads the tiers, builds the jar, the factory, the controller and the guide, and enables the controller.
        /// </summary>
        /// <param name="firstTier">Tier of the first held piece.</param>
        private IEnumerator SetUp(int firstTier)
        {
            _assets = new AssetService();
            var loading = LoadTiersAsync();
            yield return new WaitUntil(() => loading.IsCompleted);
            Assert.IsFalse(loading.IsFaulted, loading.Exception?.ToString());

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _created.Add(_config);
            TestReflection.SetField(_config, "_jarSize", new Vector2(10f, 12.5f));
            TestReflection.SetField(_config, "_dropLineOffset", 1.5f);

            var jarObject = new GameObject("Jar");
            _created.Add(jarObject);
            _jar = jarObject.AddComponent<Jar>();
            JarBuilder.Build(_jar, _config);

            var container = new GameObject("PieceContainer");
            _created.Add(container);
            var pieceGuid = UnityEditor.AssetDatabase.AssetPathToGUID(PIECE_PREFAB_PATH);
            _factory = new PieceFactory(_assets, new AssetReference(pieceGuid), _config, container.transform, 8);
            var prewarm = _factory.PrewarmAsync(_tiers);
            yield return new WaitUntil(() => prewarm.IsCompleted);
            Assert.IsFalse(prewarm.IsFaulted, prewarm.Exception?.ToString());

            _input = new ScriptedDropInput();
            var controllerObject = new GameObject("DropController");
            _created.Add(controllerObject);
            _controller = controllerObject.AddComponent<DropController>();
            var queue = new SpawnQueue(new SpawnSettings(new[] { 30f, 28f, 20f, 14f, 8f }, 5, 3, new[] { firstTier }), 12345);
            _controller.Initialize(_input, _jar, _factory, queue, _tiers, _config);

            var guide = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(GUIDE_PREFAB_PATH));
            _created.Add(guide);
            _view = guide.GetComponent<GuideLineView>();
            _view.Initialize(_controller, _jar, _tiers);

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
            while (Mathf.Abs(_controller.HeldPiece.Rigidbody.position.x - x) > 0.01f)
            {
                Assert.Less(elapsed, WAIT_LIMIT_SECONDS, "The held piece never reached the pointer.");
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
        }

        /// <summary>
        /// Waits until a released piece is falling, so a piece that has not moved yet is not taken for a settled one.
        /// </summary>
        /// <param name="piece">The piece to wait for.</param>
        private static IEnumerator WaitUntilFalling(Piece piece)
        {
            var elapsed = 0f;
            while (piece.Rigidbody.linearVelocity.y > -1f)
            {
                Assert.Less(elapsed, WAIT_LIMIT_SECONDS, "The piece never started to fall.");
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
        }

        /// <summary>
        /// Waits, in physics steps, until a piece has stayed still for several steps in a row (not just at the top
        /// of a bounce), and fails if it takes too long.
        /// </summary>
        /// <param name="piece">The piece to wait for.</param>
        private static IEnumerator WaitUntilSettled(Piece piece)
        {
            var elapsed = 0f;
            var still = 0;
            while (still < SETTLED_STEPS)
            {
                Assert.Less(elapsed, WAIT_LIMIT_SECONDS, "The piece never settled.");
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                still = piece.IsSettled ? still + 1 : 0;
            }
        }
    }
}
#endif
