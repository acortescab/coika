using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the <see cref="OverflowDetector"/> against real pieces and physics (issue #9): the 2.0 s rule, a piece
    /// that falls through the zone, the reset of the timer, the grace of merged pieces, the progress, the Danger
    /// Line, pause and restart, and the allocation budget. The physics is stepped by hand and the detector is
    /// evaluated with a simulated clock, so the tests are exact and do not wait for real time.
    /// </summary>
    public class OverflowDetectorPlayModeTests
    {
        private const float TICK = OverflowDetector.EVALUATION_INTERVAL;
        private const float OVERFLOW_TIME = 2f;
        private const float GRACE = 1f;
        private const float TOLERANCE = 0.0001f;
        private const int TIER = 2;
        private const int CRAMMED_PIECES = 60;

        private MergeTestWorld _world;
        private OverflowDetector _detector;
        private GameObject _detectorObject;
        private int _gameOvers;
        private float _clock;
        private float _lastProgress;
        private int _progressEvents;

        /// <summary>
        /// Counts the events of every test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _gameOvers = 0;
            _progressEvents = 0;
            _lastProgress = 0f;
        }

        /// <summary>
        /// Destroys the world, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_detectorObject != null)
            {
                Object.Destroy(_detectorObject);
            }

            _world?.Dispose();
            _world = null;
        }

        /// <summary>
        /// A settled piece above the line triggers the game over after 2.0 s, give or take one evaluation, and only once.
        /// </summary>
        [Test]
        public void Evaluate_SettledPieceAboveTheLine_TriggersGameOverOnceAfterTwoSeconds()
        {
            StartWorld(4);
            CreateAboveTheLine();

            var seconds = TickUntilGameOver(100);
            TickFor(3f);

            Assert.AreEqual(OVERFLOW_TIME, seconds, TICK + TOLERANCE);
            Assert.AreEqual(1, _gameOvers);
            Assert.IsTrue(_detector.HasTriggered);
        }

        /// <summary>
        /// A piece dropped from the Drop Line falls through the zone above the line, and never ends the game.
        /// </summary>
        [Test]
        public void Evaluate_PieceFallingFromTheDropLine_DoesNotTriggerGameOver()
        {
            StartWorld(4);
            var piece = _world.Factory.Create(_world.Tiers[TIER], new Vector2(0f, _world.Jar.DropLineY), Vector2.zero);
            _clock = Time.time;
            var sawPieceAboveTheLine = false;

            // Long enough for the piece to cross the line, bounce and rest on the floor.
            for (var step = 1; step <= 300; step++)
            {
                Physics2D.Simulate(Time.fixedDeltaTime);
                if (step % 5 == 0)
                {
                    _clock += 5 * Time.fixedDeltaTime;
                    _detector.Evaluate(5 * Time.fixedDeltaTime, _clock);
                }

                sawPieceAboveTheLine |= piece.Collider.bounds.max.y > _world.Jar.DangerLineY;
            }

            Assert.IsTrue(sawPieceAboveTheLine, "The piece must have crossed the zone for this test to mean something.");
            Assert.AreEqual(0, _gameOvers);
            Assert.AreEqual(0f, _detector.WorstOverflowProgress);
        }

        /// <summary>
        /// Moving the piece below the line at 1.5 s resets its timer: no game over, and a new full countdown is needed.
        /// </summary>
        [Test]
        public void Evaluate_PieceLeavesTheZoneAtOneAndAHalfSeconds_ResetsTheTimer()
        {
            StartWorld(4);
            var piece = CreateAboveTheLine();
            var above = piece.Rigidbody.position;

            TickFor(1.5f);
            Assert.AreEqual(0, _gameOvers);
            MovePiece(piece, new Vector2(above.x, _world.Jar.DangerLineY - 3f));
            TickFor(TICK);

            Assert.AreEqual(0f, piece.OverflowSeconds);
            Assert.AreEqual(0f, _detector.WorstOverflowProgress);

            MovePiece(piece, above);
            TickFor(1.8f);
            Assert.AreEqual(0, _gameOvers, "1.8 s of the new countdown is not enough.");

            var rest = TickUntilGameOver(10);
            Assert.AreEqual(1, _gameOvers);
            Assert.Less(rest, 0.5f);
        }

        /// <summary>
        /// A piece that a merge created above the line counts only after its 1.0 s grace.
        /// </summary>
        [Test]
        public void Evaluate_PieceCreatedByAMergeAboveTheLine_GetsOneSecondOfGraceBeforeCounting()
        {
            StartWorld(6);
            var y = _world.Jar.DangerLineY + 2f;
            var radius = _world.Tiers[TIER].DiameterUnits * 0.5f;
            _world.CreateFloating(TIER, new Vector2(-radius * 0.95f, y), Vector2.zero);
            _world.CreateFloating(TIER, new Vector2(radius * 0.95f, y), Vector2.zero);
            var mergeTime = Time.time;
            for (var i = 0; i < 30 && _world.CountActive(TIER + 1) == 0; i++)
            {
                _world.Step();
            }

            Assert.AreEqual(1, _world.CountActive(TIER + 1), "The pair must have merged.");
            var created = _world.Factory.ActivePieces[0];

            // 0.5 s in: the piece counts neither for the age nor for the merge grace.
            _clock = mergeTime + 0.5f;
            _detector.Evaluate(TICK, _clock);
            Assert.AreEqual(0f, created.OverflowSeconds);

            // Just after the grace it starts counting, and the full time is still needed from there.
            _clock = mergeTime + GRACE + TICK;
            TickFor(1.8f);
            Assert.AreEqual(0, _gameOvers);
            TickUntilGameOver(10);
            Assert.AreEqual(1, _gameOvers);
        }

        /// <summary>
        /// The worst progress rises linearly from 0 to 1 over 2 s, and returns to 0 when the piece is cleared.
        /// </summary>
        [Test]
        public void WorstOverflowProgress_WhileOverflowing_RisesLinearlyAndReturnsToZero()
        {
            StartWorld(4);
            var piece = CreateAboveTheLine();

            for (var i = 1; i <= 19; i++)
            {
                TickFor(TICK);
                Assert.AreEqual(i * TICK / OVERFLOW_TIME, _detector.WorstOverflowProgress, 0.001f, $"Progress after {i} ticks.");
                Assert.AreEqual(_detector.WorstOverflowProgress, _lastProgress, "The event carries the progress.");
            }

            MovePiece(piece, new Vector2(0f, _world.Jar.DangerLineY - 3f));
            TickFor(TICK);

            Assert.AreEqual(0f, _detector.WorstOverflowProgress);
            Assert.AreEqual(0f, _lastProgress);
            Assert.AreEqual(20, _progressEvents, "19 rises and one return to 0, nothing in between.");
        }

        /// <summary>
        /// A piece that is still moving, or held, never counts.
        /// </summary>
        [Test]
        public void Evaluate_MovingOrHeldPiece_DoesNotCount()
        {
            StartWorld(4);
            var moving = _world.CreateFloating(TIER, new Vector2(-2f, _world.Jar.DangerLineY + 2f), new Vector2(0f, 1f));
            var held = _world.CreateFloating(TIER, new Vector2(2f, _world.Jar.DangerLineY + 2f), Vector2.zero);
            held.SetHeld(true);
            Physics2D.SyncTransforms();
            _clock = Time.time + GRACE + 0.5f;

            TickFor(3f);

            Assert.AreEqual(0f, moving.OverflowSeconds);
            Assert.AreEqual(0f, held.OverflowSeconds);
            Assert.AreEqual(0, _gameOvers);
        }

        /// <summary>
        /// A piece completely below the line, even if it is resting close to it, does not count.
        /// </summary>
        [Test]
        public void Evaluate_PieceJustBelowTheLine_DoesNotCount()
        {
            StartWorld(4);
            var radius = _world.Tiers[TIER].DiameterUnits * 0.5f;
            _world.CreateFloating(TIER, new Vector2(0f, _world.Jar.DangerLineY - radius - 0.01f), Vector2.zero);
            Physics2D.SyncTransforms();
            _clock = Time.time + GRACE + 0.5f;

            TickFor(3f);

            Assert.AreEqual(0, _gameOvers);
        }

        /// <summary>
        /// Two pieces overflowing at once raise a single game over.
        /// </summary>
        [Test]
        public void Evaluate_TwoPiecesOverflowing_RaisesGameOverOnce()
        {
            StartWorld(4);
            CreateAboveTheLine(-2f);
            CreateAboveTheLine(2f);

            TickUntilGameOver(100);
            TickFor(2f);

            Assert.AreEqual(1, _gameOvers);
        }

        /// <summary>
        /// A piece released to the pool while it is being tracked leaves no time behind: the piece that reuses it starts at 0.
        /// </summary>
        [Test]
        public void Evaluate_PieceReleasedWhileTracked_LeavesNoStaleTime()
        {
            StartWorld(1);
            var piece = CreateAboveTheLine();
            TickFor(1.5f);
            Assert.Greater(piece.OverflowSeconds, 1f);

            _world.Factory.Release(piece);
            TickFor(TICK);
            Assert.AreEqual(0f, _detector.WorstOverflowProgress);

            var reused = _world.CreateFloating(TIER, new Vector2(0f, _world.Jar.DangerLineY + 2f), Vector2.zero);

            Assert.AreSame(piece, reused, "The pool must hand the same piece out again for this test to mean something.");
            Assert.AreEqual(0f, reused.OverflowSeconds);
        }

        /// <summary>
        /// The Danger Line shows when a piece is within the warning distance and pulses only while a piece overflows.
        /// </summary>
        [Test]
        public void DangerLine_FollowsTheDistanceAndTheOverflow()
        {
            StartWorld(4);
            var line = _world.Jar.DangerLine;
            var radius = _world.Tiers[TIER].DiameterUnits * 0.5f;
            var lineY = _world.Jar.DangerLineY;
            _clock = Time.time + GRACE + 0.5f;

            var piece = _world.CreateFloating(TIER, new Vector2(0f, lineY - 3f - radius), Vector2.zero);
            Physics2D.SyncTransforms();
            TickFor(TICK);
            Assert.IsFalse(line.IsVisible, "3 units away is too far.");

            MovePiece(piece, new Vector2(0f, lineY - 1.5f - radius));
            TickFor(TICK);
            Assert.IsTrue(line.IsVisible, "1.5 units away shows the line.");
            Assert.IsFalse(line.IsPulsing, "Near is not overflowing.");

            MovePiece(piece, new Vector2(0f, lineY + 1f));
            TickFor(TICK);
            Assert.IsTrue(line.IsVisible);
            Assert.IsTrue(line.IsPulsing, "Settled above the line pulses.");

            MovePiece(piece, new Vector2(0f, lineY - 3f - radius));
            TickFor(TICK);
            Assert.IsFalse(line.IsVisible);
            Assert.IsFalse(line.IsPulsing);
        }

        /// <summary>
        /// While disabled the timers do not advance; enabling again continues from where they were.
        /// </summary>
        [Test]
        public void Disable_FreezesTheTimers()
        {
            StartWorld(4);
            var piece = CreateAboveTheLine();
            TickFor(1f);
            var before = piece.OverflowSeconds;

            _detector.Disable();
            TickFor(5f);

            Assert.AreEqual(before, piece.OverflowSeconds);
            Assert.AreEqual(0, _gameOvers);

            _detector.Enable();
            TickFor(TICK);
            Assert.AreEqual(before + TICK, piece.OverflowSeconds, TOLERANCE);
        }

        /// <summary>
        /// A new run starts clean: no timers, no progress, no game over flag, and the Danger Line hidden. A new
        /// overflow can trigger the game over again.
        /// </summary>
        [Test]
        public void ResetForNewRun_ClearsEverythingOfThePreviousRun()
        {
            StartWorld(4);
            var piece = CreateAboveTheLine();
            TickUntilGameOver(100);
            Assert.AreEqual(1, _gameOvers);
            Assert.IsTrue(_world.Jar.DangerLine.IsPulsing);

            _detector.ResetForNewRun();

            Assert.AreEqual(0f, piece.OverflowSeconds);
            Assert.AreEqual(0f, _detector.WorstOverflowProgress);
            Assert.AreEqual(0f, _lastProgress);
            Assert.IsFalse(_detector.HasTriggered);
            Assert.IsFalse(_world.Jar.DangerLine.IsVisible);
            Assert.IsFalse(_world.Jar.DangerLine.IsPulsing);

            TickUntilGameOver(100);
            Assert.AreEqual(2, _gameOvers);
        }

        /// <summary>
        /// Enabling before initializing is a setup error.
        /// </summary>
        [Test]
        public void Enable_BeforeInitialize_Throws()
        {
            _detectorObject = new GameObject("OverflowDetector", typeof(OverflowDetector));

            Assert.Throws<System.InvalidOperationException>(() => _detectorObject.GetComponent<OverflowDetector>().Enable());
        }

        /// <summary>
        /// Evaluating 60 pieces, some overflowing and some near the line, allocates nothing.
        /// </summary>
        [Test]
        public void Evaluate_With60Pieces_AllocatesNothing()
        {
            StartWorld(CRAMMED_PIECES);
            var lineY = _world.Jar.DangerLineY;
            for (var i = 0; i < CRAMMED_PIECES; i++)
            {
                var x = -4f + (i % 10) * 0.9f;
                var y = lineY - 4f + (i / 10) * 1.5f;
                _world.CreateFloating(0, new Vector2(x, y), Vector2.zero);
            }

            Physics2D.SyncTransforms();
            _clock = Time.time + GRACE + 0.5f;
            _detector.Evaluate(TICK, _clock); // Warm-up, so the first-call costs are not measured.

            var allocations = AllocationMeter.Measure(() => _detector.Evaluate(TICK, _clock));

            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT, "Allocations in one evaluation of 60 pieces.");
        }

        /// <summary>
        /// Builds the world, the detector and the counters, and takes the physics under script control.
        /// </summary>
        /// <param name="prewarmCount">Pieces the pool builds up front.</param>
        private void StartWorld(int prewarmCount)
        {
            _world = new MergeTestWorld(prewarmCount);
            var start = _world.StartAsync();
            Assert.IsTrue(start.IsCompleted && !start.IsFaulted, start.Exception?.ToString());
            _world.UseScriptedPhysics();

            _detectorObject = new GameObject("OverflowDetector");
            _detector = _detectorObject.AddComponent<OverflowDetector>();
            _detector.Initialize(_world.Factory, _world.Jar, _world.Config);
            _detector.GameOverTriggered += () => _gameOvers++;
            _detector.OverflowProgressChanged += progress =>
            {
                _lastProgress = progress;
                _progressEvents++;
            };
            _detector.Enable();
        }

        /// <summary>
        /// Creates a settled piece with its top above the line and sets the clock past its overflow grace.
        /// </summary>
        /// <param name="x">Horizontal position.</param>
        private Piece CreateAboveTheLine(float x = 0f)
        {
            var piece = _world.CreateFloating(TIER, new Vector2(x, _world.Jar.DangerLineY + 2f), Vector2.zero);
            Physics2D.SyncTransforms();
            _clock = piece.SpawnTime + GRACE + 0.5f;
            return piece;
        }

        /// <summary>
        /// Moves a floating piece to a position and updates the physics so its collider bounds follow.
        /// </summary>
        private static void MovePiece(Piece piece, Vector2 position)
        {
            piece.Rigidbody.position = position;
            piece.transform.position = position;
            Physics2D.SyncTransforms();
        }

        /// <summary>
        /// Evaluates once per tick for the given simulated time.
        /// </summary>
        private void TickFor(float seconds)
        {
            var ticks = Mathf.RoundToInt(seconds / TICK);
            for (var i = 0; i < ticks; i++)
            {
                _clock += TICK;
                _detector.Evaluate(TICK, _clock);
            }
        }

        /// <summary>
        /// Evaluates tick by tick until the game over is raised or the tick budget runs out.
        /// </summary>
        /// <param name="maxTicks">Most ticks to evaluate.</param>
        /// <returns>Simulated seconds it took.</returns>
        private float TickUntilGameOver(int maxTicks)
        {
            var seconds = 0f;
            var raisedBefore = _gameOvers;
            for (var i = 0; i < maxTicks && _gameOvers == raisedBefore; i++)
            {
                _clock += TICK;
                seconds += TICK;
                _detector.Evaluate(TICK, _clock);
            }

            return seconds;
        }
    }
}
