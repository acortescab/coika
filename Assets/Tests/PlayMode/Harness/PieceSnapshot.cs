namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// A piece on the board at the end of a simulation: its tier and its position rounded to hundredths of a unit,
    /// kept as integers so two snapshots are equal exactly when they print the same.
    /// </summary>
    public readonly struct PieceSnapshot
    {
        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="tier">Tier index of the piece.</param>
        /// <param name="xHundredths">X position in hundredths of a world unit.</param>
        /// <param name="yHundredths">Y position in hundredths of a world unit.</param>
        public PieceSnapshot(int tier, int xHundredths, int yHundredths)
        {
            Tier = tier;
            XHundredths = xHundredths;
            YHundredths = yHundredths;
        }

        /// <summary>Tier index of the piece.</summary>
        public int Tier { get; }

        /// <summary>X position in hundredths of a world unit.</summary>
        public int XHundredths { get; }

        /// <summary>Y position in hundredths of a world unit.</summary>
        public int YHundredths { get; }
    }
}
