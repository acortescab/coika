using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// What a simulated run ended with: the score, the highest tier, the merges, the drops, whether the game is
    /// over, the simulated time and every piece left on the board. Two results are the same exactly when their text
    /// (<see cref="ToString"/>) is the same, which is how the determinism tests compare them byte for byte and how
    /// the golden file stores them. Positions are rounded to hundredths, so the text is stable.
    /// </summary>
    public sealed class SimulationResult
    {
        private readonly string _text;

        /// <summary>
        /// Creates a result.
        /// </summary>
        /// <param name="seed">Seed of the first run of the simulation.</param>
        /// <param name="score">Score of the last run.</param>
        /// <param name="highestTier">Highest tier reached in the last run.</param>
        /// <param name="merges">Merges of the last run.</param>
        /// <param name="piecesDropped">Pieces dropped in the whole simulation, restarts included.</param>
        /// <param name="gameOver">Whether the last run ended in a game over.</param>
        /// <param name="restarts">Runs started after a game over.</param>
        /// <param name="elapsedMilliseconds">Simulated time in milliseconds.</param>
        /// <param name="pieces">The pieces on the board, ordered by creation.</param>
        public SimulationResult(int seed, int score, int highestTier, int merges, int piecesDropped, bool gameOver, int restarts, long elapsedMilliseconds, IReadOnlyList<PieceSnapshot> pieces)
        {
            Seed = seed;
            Score = score;
            HighestTier = highestTier;
            Merges = merges;
            PiecesDropped = piecesDropped;
            GameOver = gameOver;
            Restarts = restarts;
            ElapsedMilliseconds = elapsedMilliseconds;
            Pieces = pieces;
            _text = Format();
        }

        /// <summary>Seed of the first run of the simulation.</summary>
        public int Seed { get; }

        /// <summary>Score of the last run.</summary>
        public int Score { get; }

        /// <summary>Highest tier reached in the last run.</summary>
        public int HighestTier { get; }

        /// <summary>Merges of the last run.</summary>
        public int Merges { get; }

        /// <summary>Pieces dropped in the whole simulation, restarts included.</summary>
        public int PiecesDropped { get; }

        /// <summary>Whether the last run ended in a game over.</summary>
        public bool GameOver { get; }

        /// <summary>Runs started after a game over.</summary>
        public int Restarts { get; }

        /// <summary>Simulated time in milliseconds.</summary>
        public long ElapsedMilliseconds { get; }

        /// <summary>The pieces on the board, ordered by creation. The piece held at the Drop Line is not on the board.</summary>
        public IReadOnlyList<PieceSnapshot> Pieces { get; }

        /// <summary>
        /// The text of the result without the seed header, to compare two runs that were started with different seeds.
        /// </summary>
        public string OutcomeText => _text.Substring(_text.IndexOf(" score=", StringComparison.Ordinal));


        /// <summary>
        /// The canonical text of the result: one header line and one line per piece, with invariant formatting.
        /// </summary>
        /// <returns>The text compared by the determinism tests and stored in the golden file.</returns>
        public override string ToString()
        {
            return _text;
        }

        /// <summary>
        /// Builds the canonical text.
        /// </summary>
        /// <returns>The text of the result.</returns>
        private string Format()
        {
            var builder = new StringBuilder();
            builder.Append("seed=").Append(Seed.ToString(CultureInfo.InvariantCulture));
            builder.Append(" score=").Append(Score.ToString(CultureInfo.InvariantCulture));
            builder.Append(" highestTier=").Append(HighestTier.ToString(CultureInfo.InvariantCulture));
            builder.Append(" merges=").Append(Merges.ToString(CultureInfo.InvariantCulture));
            builder.Append(" dropped=").Append(PiecesDropped.ToString(CultureInfo.InvariantCulture));
            builder.Append(" gameOver=").Append(GameOver ? "true" : "false");
            builder.Append(" restarts=").Append(Restarts.ToString(CultureInfo.InvariantCulture));
            builder.Append(" elapsedMs=").Append(ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            builder.Append(" pieces=").Append(Pieces.Count.ToString(CultureInfo.InvariantCulture));

            for (var i = 0; i < Pieces.Count; i++)
            {
                var piece = Pieces[i];
                builder.Append('\n');
                builder.Append("piece tier=").Append(piece.Tier.ToString(CultureInfo.InvariantCulture));
                builder.Append(" x=").Append(FormatHundredths(piece.XHundredths));
                builder.Append(" y=").Append(FormatHundredths(piece.YHundredths));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Prints hundredths of a unit as a decimal with two digits.
        /// </summary>
        /// <param name="hundredths">The value in hundredths.</param>
        /// <returns>For example <c>-1.05</c> for -105.</returns>
        private static string FormatHundredths(int hundredths)
        {
            return (hundredths / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
