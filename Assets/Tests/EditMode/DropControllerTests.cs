using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks <see cref="DropController"/> (issue #6) with a fake input, a fake asset service and exact time steps:
    /// holding, the capped and clamped follow, the release, the cooldown, the queue contract, Disable and
    /// ResetForNewRun, and the zero-allocation criterion.
    /// </summary>
    public class DropControllerTests
    {
        private const float STEP = 0.02f;
        private const float TOLERANCE = 0.0001f;
        private const float MAX_SPEED = 40f;

        private readonly List<UnityEngine.Object> _created = new();
        private FakeAssetService _assets;
        private GameConfig _config;
        private Jar _jar;
        private Transform _container;
        private List<TierDefinition> _tiers;
        private PieceFactory _factory;
        private FakeDropInput _input;
        private DropController _controller;

        /// <summary>
        /// Builds a jar, five tiers (diameters 1 to 5), a pre-warmed factory and a controller that is not enabled yet.
        /// </summary>
        [SetUp]
        public async Task SetUp()
        {
            _assets = new FakeAssetService { Provider = type => PieceFixtures.ProvideAsset(type, _created) };
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, null);
            _jar = new GameObject("Jar").AddComponent<Jar>();
            JarBuilder.Build(_jar, _config);
            _container = new GameObject("PieceContainer").transform;
            _tiers = new List<TierDefinition> { PieceFixtures.CreateTier(0, 1f, _created), PieceFixtures.CreateTier(1, 2f, _created), PieceFixtures.CreateTier(2, 3f, _created), PieceFixtures.CreateTier(3, 4f, _created), PieceFixtures.CreateTier(4, 5f, _created) };
            _factory = new PieceFactory(_assets, new AssetReference("piece-prefab"), _config, _container, 8);
            await _factory.PrewarmAsync(_tiers);
            _input = new FakeDropInput();
            _controller = new GameObject("DropController").AddComponent<DropController>();
        }

        /// <summary>
        /// Destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_controller.gameObject);
            _factory.Dispose();
            _assets.Cleanup();

            foreach (var created in _created)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
            UnityEngine.Object.DestroyImmediate(_container.gameObject);
            UnityEngine.Object.DestroyImmediate(_jar.gameObject);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// Initializes the controller with a queue that has the given settings, and enables it.
        /// </summary>
        /// <param name="settings">Spawn values of the queue. Null for the GDD ones (opening 0, 1, 0).</param>
        /// <returns>The queue the controller uses.</returns>
        private SpawnQueue Start(SpawnSettings settings = null)
        {
            var queue = new SpawnQueue(settings ?? TestSpawnSettings.Default(), TestSpawnSettings.SEED);
            _controller.Initialize(_input, _jar, _factory, queue, _tiers, _config);
            _controller.Enable();
            return queue;
        }

        /// <summary>
        /// Advances the controller a number of exact steps, each an Update and a FixedUpdate.
        /// </summary>
        /// <param name="steps">Number of steps of 0.02 s.</param>
        private void Run(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                _controller.Tick(STEP);
                _controller.FixedTick(STEP);
            }
        }

        /// <summary>
        /// Enabling holds the current piece of the queue at the Drop Line, kinematic, on the held layer and at full
        /// size, and does not advance the queue.
        /// </summary>
        [Test]
        public void Enable_Always_HoldsTheCurrentPieceAtTheDropLine()
        {
            var queue = Start();

            var held = _controller.HeldPiece;
            Assert.IsNotNull(held);
            Assert.AreEqual(queue.Current, held.Tier.Index);
            Assert.IsTrue(held.IsHeld);
            Assert.AreEqual(LayerMask.NameToLayer(Piece.HELD_LAYER_NAME), held.gameObject.layer);
            Assert.AreEqual(_jar.DropLineY, held.transform.position.y, TOLERANCE);
            Assert.AreEqual(Vector3.one, held.transform.localScale);
            Assert.AreEqual(DropState.Aiming, _controller.State);
            Assert.AreEqual(1, queue.Next, "Enabling does not advance the queue.");
        }

        /// <summary>
        /// Using the controller before Initialize is a programming error.
        /// </summary>
        [Test]
        public void Enable_BeforeInitialize_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _controller.Enable());
        }

        /// <summary>
        /// A missing dependency is a programming error.
        /// </summary>
        [Test]
        public void Initialize_WithANullDependency_Throws()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), 1);

            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(null, _jar, _factory, queue, _tiers, _config), "input");
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_input, null, _factory, queue, _tiers, _config), "jar");
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_input, _jar, null, queue, _tiers, _config), "factory");
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_input, _jar, _factory, null, _tiers, _config), "queue");
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_input, _jar, _factory, queue, null, _config), "tiers");
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(_input, _jar, _factory, queue, _tiers, null), "config");
        }

        /// <summary>
        /// A queue that asks for a tier with no definition fails clearly.
        /// </summary>
        [Test]
        public void Enable_WithATierThatHasNoDefinition_Throws()
        {
            var queue = new SpawnQueue(TestSpawnSettings.For(TestSpawnSettings.DefaultWeights, 3, 3), 1);
            _controller.Initialize(_input, _jar, _factory, queue, _tiers.GetRange(0, 1), _config);

            Assert.Throws<InvalidOperationException>(() => _controller.Enable());
        }

        /// <summary>
        /// A pointer that jumps far away moves the piece no more than the maximum speed allows in one step.
        /// </summary>
        [Test]
        public void FixedTick_WithAPointerFarAway_MovesNoMoreThanTheMaximumSpeed()
        {
            Start();
            _input.HasPointer = true;
            _input.PointerWorldX = 1000f;

            _controller.FixedTick(STEP);

            Assert.AreEqual(MAX_SPEED * STEP, _controller.HeldX, TOLERANCE);
        }

        /// <summary>
        /// Over many steps the speed never goes above the maximum, even when the pointer jumps from one side to the other.
        /// </summary>
        [Test]
        public void FixedTick_WithAPointerJumpingAcrossTheScreen_NeverExceedsTheMaximumSpeed()
        {
            Start();
            _input.HasPointer = true;

            for (int i = 0; i < 50; i++)
            {
                _input.PointerWorldX = i % 2 == 0 ? 1000f : -1000f;
                var before = _controller.HeldX;

                _controller.FixedTick(STEP);

                Assert.LessOrEqual(Mathf.Abs(_controller.HeldX - before) / STEP, MAX_SPEED + TOLERANCE, $"step {i}");
            }
        }

        /// <summary>
        /// The smallest piece stops at both walls with its whole body inside the jar.
        /// </summary>
        [Test]
        public void FixedTick_WithTheSmallestPieceAgainstTheWalls_StaysInsideTheJar()
        {
            Start(TestSpawnSettings.For(TestSpawnSettings.DefaultWeights, 3, 0));
            _input.HasPointer = true;

            _input.PointerWorldX = 1000f;
            Run(60);
            Assert.AreEqual(_jar.InteriorMax.x - 0.5f, _controller.HeldX, TOLERANCE, "right wall");

            _input.PointerWorldX = -1000f;
            Run(60);
            Assert.AreEqual(_jar.InteriorMin.x + 0.5f, _controller.HeldX, TOLERANCE, "left wall");
        }

        /// <summary>
        /// The largest spawnable piece (tier 4) stops at both walls with its whole body inside the jar.
        /// </summary>
        [Test]
        public void FixedTick_WithTheLargestPieceAgainstTheWalls_StaysInsideTheJar()
        {
            Start(TestSpawnSettings.For(TestSpawnSettings.DefaultWeights, 3, 4));
            Assert.AreEqual(4, _controller.HeldPiece.Tier.Index);
            _input.HasPointer = true;

            _input.PointerWorldX = 1000f;
            Run(60);
            Assert.AreEqual(_jar.InteriorMax.x - 2.5f, _controller.HeldX, TOLERANCE, "right wall");

            _input.PointerWorldX = -1000f;
            Run(60);
            Assert.AreEqual(_jar.InteriorMin.x + 2.5f, _controller.HeldX, TOLERANCE, "left wall");
        }

        /// <summary>
        /// The keyboard moves the piece at the same maximum speed as the pointer.
        /// </summary>
        [Test]
        public void FixedTick_WithTheKeyboardAxis_MovesAtTheMaximumSpeed()
        {
            Start();
            _input.MoveAxis = 1f;

            _controller.FixedTick(STEP);

            Assert.AreEqual(MAX_SPEED * STEP, _controller.HeldX, TOLERANCE);
        }

        /// <summary>
        /// With no pointer and no key the piece stays where it is.
        /// </summary>
        [Test]
        public void FixedTick_WithNoInput_KeepsThePiecePut()
        {
            Start();

            Run(10);

            Assert.AreEqual(0f, _controller.HeldX, TOLERANCE);
        }

        /// <summary>
        /// A release drops the piece: it is no longer held, it keeps full size, and PieceDropped reports its tier.
        /// </summary>
        [Test]
        public void Tick_AfterAClick_DropsThePieceAndRaisesPieceDropped()
        {
            Start();
            var dropped = _controller.HeldPiece;
            var droppedTiers = new List<int>();
            _controller.PieceDropped += droppedTiers.Add;

            _input.Click();
            _controller.Tick(STEP);

            Assert.IsFalse(dropped.IsHeld);
            Assert.AreEqual(LayerMask.NameToLayer(Piece.LAYER_NAME), dropped.gameObject.layer);
            Assert.AreEqual(Vector2.zero, dropped.Rigidbody.linearVelocity, "It starts with no velocity.");
            Assert.AreEqual(Vector3.one, dropped.transform.localScale);
            CollectionAssert.AreEqual(new[] { 0 }, droppedTiers);
        }

        /// <summary>
        /// The queue advances once, on the release, and the piece that was shown as Next is the one that appears.
        /// </summary>
        [Test]
        public void Tick_AfterAClick_AdvancesTheQueueOnceAndHoldsThePreviewedPiece()
        {
            var queue = Start();
            var previewed = queue.Next;

            _input.Click();
            _controller.Tick(STEP);

            Assert.AreEqual(previewed, queue.Current, "The queue moved one piece.");
            Assert.AreEqual(previewed, _controller.HeldPiece.Tier.Index, "The previewed piece appears.");
            Assert.IsTrue(_controller.HeldPiece.IsHeld);
        }

        /// <summary>
        /// Nothing advances the queue until a release, however long the controller aims.
        /// </summary>
        [Test]
        public void Tick_WhileAiming_NeverAdvancesTheQueue()
        {
            var queue = Start();
            var next = queue.Next;

            Run(100);

            Assert.AreEqual(next, queue.Next);
            Assert.AreEqual(0, queue.GetState().AdvanceCount);
        }

        /// <summary>
        /// The substate becomes Dropping on the release and Aiming when the cooldown ends, and each change is announced.
        /// </summary>
        [Test]
        public void Tick_AfterAClick_ChangesTheStateAndRaisesStateChanged()
        {
            Start();
            var states = new List<DropState>();
            _controller.StateChanged += states.Add;

            _input.Click();
            _controller.Tick(STEP);
            Assert.AreEqual(DropState.Dropping, _controller.State);

            Run(25);

            Assert.AreEqual(DropState.Aiming, _controller.State);
            CollectionAssert.AreEqual(new[] { DropState.Dropping, DropState.Aiming }, states);
        }

        /// <summary>
        /// During the cooldown the next piece does not follow the input and a click does nothing.
        /// </summary>
        [Test]
        public void Tick_DuringTheCooldown_IgnoresTheInput()
        {
            var queue = Start();
            _input.Click();
            _controller.Tick(STEP);
            var heldAfterDrop = _controller.HeldPiece;
            var advances = queue.GetState().AdvanceCount;
            _input.HasPointer = true;
            _input.PointerWorldX = 4f;

            Run(10);
            _input.Click();
            _controller.Tick(STEP);

            Assert.AreEqual(0f, _controller.HeldX, TOLERANCE, "The piece does not move.");
            Assert.AreSame(heldAfterDrop, _controller.HeldPiece, "No second drop.");
            Assert.AreEqual(advances, queue.GetState().AdvanceCount);
        }

        /// <summary>
        /// The next piece grows from nothing to full size in the scale-in time while the drop cools down.
        /// </summary>
        [Test]
        public void Tick_AfterAClick_GrowsTheNextPieceFromZeroToFullSize()
        {
            Start();
            _input.Click();
            _controller.Tick(STEP);
            Assert.AreEqual(0f, _controller.HeldPiece.transform.localScale.x, TOLERANCE, "It starts with no size.");

            Run(4);
            Assert.AreEqual(0.08f / 0.15f, _controller.HeldPiece.transform.localScale.x, 0.001f, "0.08 s of 0.15 s.");

            Run(4);
            Assert.AreEqual(1f, _controller.HeldPiece.transform.localScale.x, TOLERANCE, "0.16 s is past the scale-in time.");
        }

        /// <summary>
        /// A click in the very step in which the cooldown ends drops again.
        /// </summary>
        [Test]
        public void Tick_ClickInTheStepTheCooldownEnds_Drops()
        {
            var queue = Start();
            _input.Click();
            _controller.Tick(STEP);
            Run(24);
            Assert.AreEqual(DropState.Dropping, _controller.State, "One step of the cooldown is left.");

            _input.Click();
            _controller.Tick(STEP);

            Assert.AreEqual(2, queue.GetState().AdvanceCount);
        }

        /// <summary>
        /// A press that begins during the cooldown still drops when it is released after the cooldown ends.
        /// </summary>
        [Test]
        public void Tick_PressDuringTheCooldownReleasedAfter_Drops()
        {
            var queue = Start();
            _input.Click();
            _controller.Tick(STEP);
            _input.RaisePressed();
            _controller.Tick(STEP);
            Run(25);
            Assert.AreEqual(DropState.Aiming, _controller.State);

            _input.RaiseReleased();
            _controller.Tick(STEP);

            Assert.AreEqual(2, queue.GetState().AdvanceCount);
        }

        /// <summary>
        /// A release with no press before it, such as the tail of a tap on a button, drops nothing.
        /// </summary>
        [Test]
        public void Tick_ReleaseWithNoPressBefore_DoesNotDrop()
        {
            var queue = Start();

            _input.RaiseReleased();
            _controller.Tick(STEP);

            Assert.AreEqual(0, queue.GetState().AdvanceCount);
            Assert.AreEqual(DropState.Aiming, _controller.State);
        }

        /// <summary>
        /// A touch that began during the cooldown does nothing until the cooldown ends, then the controller reports
        /// the press, for the guide, and the piece slides to the finger at the capped speed instead of jumping.
        /// </summary>
        [Test]
        public void Tick_TouchBeganDuringTheCooldownAndStillDown_SlidesThePieceToTheFingerWhenItEnds()
        {
            Start();
            _input.Click();
            _controller.Tick(STEP);
            Assert.AreEqual(DropState.Dropping, _controller.State, "The first click dropped the piece.");
            _input.HasPointer = true;
            _input.PointerWorldX = 1f;
            _input.RaisePressed();
            _controller.Tick(STEP);
            Assert.AreEqual(DropState.Dropping, _controller.State);
            Assert.IsFalse(_controller.IsPressing, "Nothing is pressing while the cooldown runs.");

            for (int i = 0; i < 40 && _controller.State != DropState.Aiming; i++)
            {
                _controller.Tick(STEP);
            }

            Assert.AreEqual(DropState.Aiming, _controller.State);
            Assert.Greater(Mathf.Abs(1f - _controller.HeldX), 0.1f, "The piece does not jump to the finger.");
            Assert.IsTrue(_controller.IsPressing, "The press is reported once the cooldown ends.");

            Run(5);

            Assert.AreEqual(1f, _controller.HeldX, TOLERANCE, "The piece reaches the finger.");
        }

        /// <summary>
        /// Two quick clicks in the same frame drop one piece.
        /// </summary>
        [Test]
        public void Tick_WithTwoClicksInOneFrame_DropsOnlyOnce()
        {
            var queue = Start();

            _input.Click();
            _input.Click();
            _controller.Tick(STEP);

            Assert.AreEqual(1, queue.GetState().AdvanceCount);
        }

        /// <summary>
        /// Disabling stops the input and gives the held piece back, so nothing stale is left.
        /// </summary>
        [Test]
        public void Disable_WhileHolding_ReleasesTheHeldPieceAndIgnoresTheInput()
        {
            var queue = Start();
            var held = _controller.HeldPiece;

            _controller.Disable();
            _input.Click();
            _controller.Tick(STEP);

            Assert.IsFalse(_controller.IsEnabled);
            Assert.IsNull(_controller.HeldPiece);
            Assert.IsFalse(held.gameObject.activeSelf, "The piece went back to the pool.");
            Assert.AreEqual(0, _factory.ActivePieces.Count);
            Assert.AreEqual(0, queue.GetState().AdvanceCount);
            Assert.AreEqual(0, _input.ListenerCount, "It stopped listening.");
        }

        /// <summary>
        /// Enabling again holds the current piece of the queue without advancing it.
        /// </summary>
        [Test]
        public void Enable_AfterDisable_HoldsTheCurrentPieceAgain()
        {
            var queue = Start();
            _controller.Disable();

            _controller.Enable();

            Assert.AreEqual(queue.Current, _controller.HeldPiece.Tier.Index);
            Assert.AreEqual(0, queue.GetState().AdvanceCount);
            Assert.AreEqual(1, _factory.ActivePieces.Count);
        }

        /// <summary>
        /// Disabling during the cooldown and enabling again returns to Aiming and announces it.
        /// </summary>
        [Test]
        public void Enable_AfterDisablingDuringTheCooldown_ReturnsToAiming()
        {
            Start();
            _input.Click();
            _controller.Tick(STEP);
            _controller.Disable();
            var states = new List<DropState>();
            _controller.StateChanged += states.Add;

            _controller.Enable();

            Assert.AreEqual(DropState.Aiming, _controller.State);
            CollectionAssert.AreEqual(new[] { DropState.Aiming }, states);
        }

        /// <summary>
        /// A new run discards the held piece and holds a fresh one from the new queue, back in Aiming.
        /// </summary>
        [Test]
        public void ResetForNewRun_AfterADrop_ReturnsToAimingWithAFreshPieceFromTheNewQueue()
        {
            Start();
            _input.Click();
            _controller.Tick(STEP);
            var newQueue = new SpawnQueue(TestSpawnSettings.For(TestSpawnSettings.DefaultWeights, 3, 3), 7);

            _factory.ReleaseAll();
            _controller.ResetForNewRun(newQueue);

            Assert.AreEqual(DropState.Aiming, _controller.State);
            Assert.AreEqual(3, _controller.HeldPiece.Tier.Index);
            Assert.IsTrue(_controller.HeldPiece.IsHeld);
            Assert.AreEqual(1, _factory.ActivePieces.Count, "Only the new held piece is in play.");
            Assert.AreEqual(0, newQueue.GetState().AdvanceCount, "Resetting does not advance the new queue.");
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// The order against ReleaseAll does not matter: resetting first and releasing the pieces after works too.
        /// </summary>
        [Test]
        public void ResetForNewRun_BeforeReleaseAll_LogsNoWarning()
        {
            Start();
            var newQueue = new SpawnQueue(TestSpawnSettings.Default(), 7);

            _controller.ResetForNewRun(newQueue);
            _factory.ReleaseAll();

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// While disabled, a reset holds nothing and the next Enable holds the piece of the new queue.
        /// </summary>
        [Test]
        public void ResetForNewRun_WhileDisabled_HoldsNothingUntilEnabled()
        {
            Start();
            _controller.Disable();
            var newQueue = new SpawnQueue(TestSpawnSettings.For(TestSpawnSettings.DefaultWeights, 3, 2), 7);

            _controller.ResetForNewRun(newQueue);
            Assert.IsNull(_controller.HeldPiece);

            _controller.Enable();
            Assert.AreEqual(2, _controller.HeldPiece.Tier.Index);
        }

        /// <summary>
        /// A missing queue is a programming error.
        /// </summary>
        [Test]
        public void ResetForNewRun_WithANullQueue_Throws()
        {
            Start();

            Assert.Throws<ArgumentNullException>(() => _controller.ResetForNewRun(null));
        }

        /// <summary>
        /// If the factory takes the held piece back (ReleaseAll), the controller notices on the next tick and holds
        /// a fresh piece, so the next click drops a live piece and not a pooled one.
        /// </summary>
        [Test]
        public void Tick_AfterReleaseAllTookTheHeldPieceBack_HoldsAFreshPiece()
        {
            var queue = Start();

            _factory.ReleaseAll();
            _controller.Tick(STEP);

            var held = _controller.HeldPiece;
            Assert.IsTrue(held.gameObject.activeSelf);
            Assert.IsTrue(held.IsHeld);
            Assert.AreEqual(queue.Current, held.Tier.Index);
            Assert.AreEqual(1, _factory.ActivePieces.Count);

            _input.Click();
            _controller.Tick(STEP);

            Assert.AreEqual(1, queue.GetState().AdvanceCount);
            Assert.IsFalse(held.IsHeld, "The piece that was dropped is a live one.");
        }

        /// <summary>
        /// A listener of PieceDropped that throws does not leave the controller without a piece: the next piece is
        /// held before the events are raised, and no press stays behind.
        /// </summary>
        [Test]
        public void Tick_WhenAPieceDroppedListenerThrows_StillHoldsTheNextPiece()
        {
            Start();
            Action<int> thrower = _ => throw new InvalidOperationException("listener failed");
            _controller.PieceDropped += thrower;
            _input.Click();

            Assert.Throws<InvalidOperationException>(() => _controller.Tick(STEP));
            _controller.PieceDropped -= thrower;

            Assert.IsNotNull(_controller.HeldPiece);
            Assert.IsTrue(_controller.HeldPiece.IsHeld);
            Assert.AreEqual(DropState.Dropping, _controller.State);
            Assert.DoesNotThrow(() => Run(30), "No press was left behind.");
            Assert.AreEqual(DropState.Aiming, _controller.State);
        }

        /// <summary>
        /// With no cooldown the drop never reports the Dropping state, because the next piece is controllable at
        /// once.
        /// </summary>
        [Test]
        public void Tick_AfterAClickWithNoCooldown_StaysAimingAndRaisesNoStateChange()
        {
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("_dropDownCooldown").floatValue = 0f;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            var queue = Start();
            var states = new List<DropState>();
            _controller.StateChanged += states.Add;

            _input.Click();
            _controller.Tick(STEP);

            Assert.AreEqual(DropState.Aiming, _controller.State);
            Assert.IsEmpty(states);
            Assert.AreEqual(1, queue.GetState().AdvanceCount);
            Assert.AreEqual(Vector3.one, _controller.HeldPiece.transform.localScale);
        }

        /// <summary>
        /// While aiming, a frame allocates no managed memory (S-50).
        /// </summary>
        [Test]
        public void TickAndFixedTick_WhileAiming_AllocateNoManagedMemory()
        {
            Start();
            _input.HasPointer = true;
            Run(50);

            var allocated = AllocationMeter.Measure(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    _input.PointerWorldX = (i % 100) * 0.05f - 2.5f;
                    _controller.Tick(STEP);
                    _controller.FixedTick(STEP);
                }
            });

            Assert.LessOrEqual(allocated, AllocationMeter.TOLERANCE_COUNT, "Managed allocations made by 1,000 aiming frames.");
        }

        /// <summary>
        /// Dropping and holding the next piece, once the pool is warm, allocates no managed memory either.
        /// </summary>
        [Test]
        public void Tick_DroppingWithAWarmPool_AllocatesNoManagedMemory()
        {
            Start();
            _input.Click();
            _controller.Tick(STEP);
            Run(30);

            var allocated = AllocationMeter.Measure(() =>
            {
                for (int i = 0; i < 4; i++)
                {
                    _input.Click();
                    _controller.Tick(STEP);
                    for (int step = 0; step < 30; step++)
                    {
                        _controller.Tick(STEP);
                        _controller.FixedTick(STEP);
                    }
                }
            });

            Assert.LessOrEqual(allocated, AllocationMeter.TOLERANCE_COUNT, "Managed allocations made by 4 drops.");
        }

        /// <summary>
        /// The offset taken when the press began is added to the pointer, so the piece keeps its place under the finger.
        /// </summary>
        [Test]
        public void FixedTick_WithAPointerOffset_FollowsThePointerPlusTheOffset()
        {
            Start();
            _input.HasPointer = true;
            _input.PointerWorldX = 1f;
            _input.PointerOffset = 2f;
            _input.RaisePressed();

            Run(10);

            Assert.AreEqual(3f, _controller.HeldX, TOLERANCE);
        }

        /// <summary>
        /// The jar bounds apply after the offset, so a finger far outside the screen never takes the piece out.
        /// </summary>
        [Test]
        public void FixedTick_WithAnOffsetAndAFingerFarOutside_StaysInsideTheJar()
        {
            Start(TestSpawnSettings.For(TestSpawnSettings.DefaultWeights, 3, 4));
            _input.HasPointer = true;
            _input.PointerOffset = 3f;
            _input.RaisePressed();

            _input.PointerWorldX = 100000f;
            Run(60);
            Assert.AreEqual(_jar.InteriorMax.x - 2.5f, _controller.HeldX, TOLERANCE, "right wall");

            _input.PointerWorldX = -100000f;
            Run(60);
            Assert.AreEqual(_jar.InteriorMin.x + 2.5f, _controller.HeldX, TOLERANCE, "left wall");
        }

        /// <summary>
        /// A cancelled press goes back to hovering: the piece is not dropped, not even by a later release.
        /// </summary>
        [Test]
        public void Tick_AfterACancelledPress_DoesNotDrop()
        {
            Start();
            var held = _controller.HeldPiece;
            var drops = 0;
            _controller.PieceDropped += _ => drops++;

            _input.RaisePressed();
            _input.RaiseCancelled();
            _controller.Tick(STEP);
            _input.RaiseReleased();
            _controller.Tick(STEP);

            Assert.AreEqual(0, drops);
            Assert.IsTrue(held.IsHeld);
            Assert.AreEqual(DropState.Aiming, _controller.State);
        }

        /// <summary>
        /// After a cancel the next press and release drops normally.
        /// </summary>
        [Test]
        public void Tick_PressAfterACancelledPress_Drops()
        {
            Start();
            var drops = 0;
            _controller.PieceDropped += _ => drops++;
            _input.RaisePressed();
            _input.RaiseCancelled();
            _controller.Tick(STEP);

            _input.Click();
            _controller.Tick(STEP);

            Assert.AreEqual(1, drops);
        }
    }
}
