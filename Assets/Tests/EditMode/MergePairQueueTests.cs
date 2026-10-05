using System.Collections.Generic;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the rules of the <see cref="MergePairQueue"/> (issue #7): which pairs are accepted, that a contact
    /// reported from both sides is queued once, that the order is stable, and that it allocates nothing.
    /// </summary>
    public class MergePairQueueTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private GameConfig _config;
        private Sprite _sprite;
        private TierDefinition _tierA;
        private TierDefinition _tierB;
        private MergePairQueue _queue;

        /// <summary>
        /// Creates two tiers, a sprite, a config and an empty queue.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = TestGameConfig.Create(new Vector2(10f, 12.5f), 1.5f, null);
            _tierA = PieceFixtures.CreateTier(0, 1f, _created);
            _tierB = PieceFixtures.CreateTier(1, 1.5f, _created);
            _sprite = (Sprite)PieceFixtures.ProvideAsset(typeof(Sprite), _created);
            _created.Add(_sprite);
            _queue = new MergePairQueue();
        }

        /// <summary>
        /// Destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// A contact reported by the lower piece is queued, with the lower piece first.
        /// </summary>
        [Test]
        public void TryEnqueue_SameTierReportedByLowerPiece_QueuesThePair()
        {
            var low = CreatePiece(_tierA, 1);
            var high = CreatePiece(_tierA, 2);

            Assert.IsTrue(_queue.TryEnqueue(low, high));

            Assert.AreEqual(1, _queue.Count);
            Assert.AreSame(low, _queue[0].Low);
            Assert.AreSame(high, _queue[0].High);
        }

        /// <summary>
        /// Both pieces of a contact report it: the report of the higher piece is ignored, and so is a repeat of the
        /// lower one, so the pair is queued once.
        /// </summary>
        [Test]
        public void TryEnqueue_ReportedFromBothSidesAndRepeated_QueuesThePairOnce()
        {
            var low = CreatePiece(_tierA, 1);
            var high = CreatePiece(_tierA, 2);

            Assert.IsTrue(_queue.TryEnqueue(low, high));
            Assert.IsFalse(_queue.TryEnqueue(high, low), "The higher piece does not own the pair.");
            Assert.IsFalse(_queue.TryEnqueue(low, high), "A repeat is ignored.");

            Assert.AreEqual(1, _queue.Count);
        }

        /// <summary>
        /// Pieces of different tiers never merge.
        /// </summary>
        [Test]
        public void TryEnqueue_DifferentTiers_IsRejected()
        {
            Assert.IsFalse(_queue.TryEnqueue(CreatePiece(_tierA, 1), CreatePiece(_tierB, 2)));
            Assert.AreEqual(0, _queue.Count);
        }

        /// <summary>
        /// A piece that was already merged, or that the player holds, is not queued.
        /// </summary>
        [Test]
        public void TryEnqueue_MergedOrHeldPiece_IsRejected()
        {
            var merged = CreatePiece(_tierA, 1);
            merged.MarkMerged();
            var held = CreatePiece(_tierA, 2);
            held.SetHeld(true);
            var free = CreatePiece(_tierA, 3);
            var other = CreatePiece(_tierA, 4);

            Assert.IsFalse(_queue.TryEnqueue(merged, free));
            Assert.IsFalse(_queue.TryEnqueue(free, merged));
            Assert.IsFalse(_queue.TryEnqueue(held, free));
            Assert.IsFalse(_queue.TryEnqueue(free, held));
            Assert.IsTrue(_queue.TryEnqueue(free, other), "Control: two free pieces do queue.");
        }

        /// <summary>
        /// Null, the same piece twice and a piece that was never initialized are rejected without throwing.
        /// </summary>
        [Test]
        public void TryEnqueue_NullSamePieceOrUninitializedPiece_IsRejected()
        {
            var piece = CreatePiece(_tierA, 1);
            var blank = new GameObject("Blank", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
            _created.Add(blank);

            Assert.IsFalse(_queue.TryEnqueue(null, piece));
            Assert.IsFalse(_queue.TryEnqueue(piece, null));
            Assert.IsFalse(_queue.TryEnqueue(piece, piece));
            Assert.IsFalse(_queue.TryEnqueue(blank.GetComponent<Piece>(), piece));
        }

        /// <summary>
        /// Pairs come out ordered by their lowest piece whatever order they were reported in, so every run merges in
        /// the same order.
        /// </summary>
        [Test]
        public void Sort_PairsReportedOutOfOrder_OrdersThemBySequenceId()
        {
            var pieces = new Piece[6];
            for (var i = 0; i < pieces.Length; i++)
            {
                pieces[i] = CreatePiece(_tierA, i);
            }

            _queue.TryEnqueue(pieces[4], pieces[5]);
            _queue.TryEnqueue(pieces[0], pieces[3]);
            _queue.TryEnqueue(pieces[2], pieces[3]);
            _queue.TryEnqueue(pieces[0], pieces[1]);

            _queue.Sort();

            Assert.AreEqual(4, _queue.Count);
            AssertPair(0, pieces[0], pieces[1]);
            AssertPair(1, pieces[0], pieces[3]);
            AssertPair(2, pieces[2], pieces[3]);
            AssertPair(3, pieces[4], pieces[5]);
        }

        /// <summary>
        /// Pieces that did not come from the factory share a sequence id: the Unity instance id breaks the tie, so
        /// exactly one of the two orders is accepted.
        /// </summary>
        [Test]
        public void TryEnqueue_EqualSequenceIds_AcceptsExactlyOneOrder()
        {
            var first = CreatePiece(_tierA, 0);
            var second = CreatePiece(_tierA, 0);

            var accepted = (_queue.TryEnqueue(first, second) ? 1 : 0) + (_queue.TryEnqueue(second, first) ? 1 : 0);

            Assert.AreEqual(1, accepted);
        }

        /// <summary>
        /// Clearing forgets the pairs, and the same pair can be queued again.
        /// </summary>
        [Test]
        public void Clear_AfterQueueing_EmptiesTheQueueAndAllowsTheSamePairAgain()
        {
            var low = CreatePiece(_tierA, 1);
            var high = CreatePiece(_tierA, 2);
            _queue.TryEnqueue(low, high);

            _queue.Clear();

            Assert.AreEqual(0, _queue.Count);
            Assert.IsTrue(_queue.TryEnqueue(low, high));
        }

        /// <summary>
        /// Enqueueing the contacts of a step, sorting and clearing allocate nothing once the list has its capacity
        /// (S-50).
        /// </summary>
        [Test]
        public void QueueingSortingAndClearing_InSteadyState_AllocatesNothing()
        {
            var pieces = new Piece[8];
            for (var i = 0; i < pieces.Length; i++)
            {
                pieces[i] = CreatePiece(_tierA, i);
            }

            RunStep(pieces);
            var allocations = AllocationMeter.MeasureLowest(() =>
            {
                for (var step = 0; step < 1000; step++)
                {
                    RunStep(pieces);
                }
            });

            // An allocation per step would make 1,000 or more; the global counter also picks up some noise of other Editor threads.
            Assert.Less(allocations, 1000 / 10);
        }

        /// <summary>
        /// Reports every contact twice, from both sides, as the physics does, then sorts and clears.
        /// </summary>
        private void RunStep(Piece[] pieces)
        {
            for (var i = pieces.Length - 1; i > 0; i--)
            {
                _queue.TryEnqueue(pieces[i], pieces[i - 1]);
                _queue.TryEnqueue(pieces[i - 1], pieces[i]);
            }

            _queue.Sort();
            _queue.Clear();
        }

        /// <summary>
        /// Checks the pair at an index of the queue.
        /// </summary>
        private void AssertPair(int index, Piece low, Piece high)
        {
            Assert.AreSame(low, _queue[index].Low, $"Pair {index}: low.");
            Assert.AreSame(high, _queue[index].High, $"Pair {index}: high.");
        }

        /// <summary>
        /// Creates an initialized piece with a sequence id.
        /// </summary>
        private Piece CreatePiece(TierDefinition tier, int sequenceId)
        {
            var pieceObject = new GameObject("Piece", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
            _created.Add(pieceObject);
            var piece = pieceObject.GetComponent<Piece>();
            piece.Initialize(tier, _sprite, _config);
            piece.AssignSequenceId(sequenceId);
            return piece;
        }
    }
}
