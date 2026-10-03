namespace Coika.Gameplay
{
    /// <summary>
    /// Two touching pieces of the same tier that will merge. <see cref="Low"/> is the one with the lower sequence id,
    /// which owns the merge.
    /// </summary>
    public readonly struct MergePair
    {
        /// <summary>The piece with the lower sequence id.</summary>
        public readonly Piece Low;

        /// <summary>The piece with the higher sequence id.</summary>
        public readonly Piece High;

        /// <summary>
        /// Creates the pair.
        /// </summary>
        /// <param name="low">The piece with the lower sequence id.</param>
        /// <param name="high">The piece with the higher sequence id.</param>
        public MergePair(Piece low, Piece high)
        {
            Low = low;
            High = high;
        }
    }
}
