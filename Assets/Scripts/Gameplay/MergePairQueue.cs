using System.Collections.Generic;

namespace Coika.Gameplay
{
    /// <summary>
    /// The pairs of touching pieces waiting to be merged in the next physics step (GDD §3.4). It holds the rules that
    /// keep merging deterministic: a pair is accepted once however many times and from whichever side it is reported,
    /// only the piece with the lower <see cref="Piece.SequenceId"/> owns it, and the pairs are handed out in a stable
    /// order. The <see cref="MergeSystem"/> fills it from the collision callbacks, which must only enqueue, and
    /// drains it in FixedUpdate.
    /// <para>
    /// The list is reused and the sort is done in place, so a step allocates nothing (S-50).
    /// </para>
    /// </summary>
    public sealed class MergePairQueue
    {
        private readonly List<MergePair> _pairs = new();

        /// <summary>Number of pairs waiting.</summary>
        public int Count => _pairs.Count;

        /// <summary>The pair at the index, in the order of the last <see cref="Sort"/>.</summary>
        /// <param name="index">Index from 0 to <see cref="Count"/> - 1.</param>
        public MergePair this[int index] => _pairs[index];

        /// <summary>
        /// Adds the pair if it can merge: both pieces exist and are different, have the same tier, are not held and
        /// are not merged yet, the pair is not already waiting, and <paramref name="reporter"/> is the lower of the
        /// two. Both pieces of a contact report it, so the report of the higher one is ignored.
        /// </summary>
        /// <param name="reporter">The piece that reports the contact.</param>
        /// <param name="other">The piece it touches.</param>
        /// <returns>True when the pair was added.</returns>
        public bool TryEnqueue(Piece reporter, Piece other)
        {
            if (reporter == null || other == null || reporter == other)
            {
                return false;
            }

            if (reporter.Tier == null || reporter.Tier != other.Tier)
            {
                return false;
            }

            if (reporter.Merged || other.Merged || reporter.IsHeld || other.IsHeld)
            {
                return false;
            }

            if (Compare(reporter, other) >= 0 || Contains(reporter, other))
            {
                return false;
            }

            _pairs.Add(new MergePair(reporter, other));
            return true;
        }

        /// <summary>
        /// Orders the pairs by the sequence ids of their pieces, lower piece first, so every run processes them in
        /// the same order whatever order the physics engine reported the contacts in. An insertion sort: the queue
        /// is short and it allocates nothing, unlike a sort with a comparison delegate.
        /// </summary>
        public void Sort()
        {
            for (var i = 1; i < _pairs.Count; i++)
            {
                var pair = _pairs[i];
                var j = i - 1;
                while (j >= 0 && Compare(pair, _pairs[j]) < 0)
                {
                    _pairs[j + 1] = _pairs[j];
                    j--;
                }

                _pairs[j + 1] = pair;
            }
        }

        /// <summary>
        /// Forgets every waiting pair.
        /// </summary>
        public void Clear()
        {
            _pairs.Clear();
        }

        /// <summary>
        /// Compares two pieces by creation order. Pieces that did not come from the factory all have the same
        /// sequence id, so the Unity entity id breaks the tie.
        /// </summary>
        private static int Compare(Piece a, Piece b)
        {
            var bySequence = a.SequenceId.CompareTo(b.SequenceId);
            return bySequence != 0 ? bySequence : a.GetEntityId().CompareTo(b.GetEntityId());
        }

        /// <summary>
        /// Compares two pairs by their lower piece, then by their higher one.
        /// </summary>
        private static int Compare(MergePair a, MergePair b)
        {
            var byLow = Compare(a.Low, b.Low);
            return byLow != 0 ? byLow : Compare(a.High, b.High);
        }

        /// <summary>
        /// Whether the pair is already waiting. A linear scan: the queue holds a handful of pairs.
        /// </summary>
        private bool Contains(Piece low, Piece high)
        {
            for (var i = 0; i < _pairs.Count; i++)
            {
                if (_pairs[i].Low == low && _pairs[i].High == high)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
