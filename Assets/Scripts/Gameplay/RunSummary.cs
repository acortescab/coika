namespace Coika.Gameplay
{
    /// <summary>
    /// Immutable snapshot of a finished run for the Game Over view (GDD §8.4). It is built once, when the run
    /// ends, from the <see cref="ScoreSystem"/> and the <see cref="RunTimer"/>, so the view never reads a system.
    /// </summary>
    public readonly struct RunSummary
    {
        /// <summary>
        /// Creates a summary from explicit values.
        /// </summary>
        /// <param name="score">Final score of the run.</param>
        /// <param name="bestScore">Best score after the run: the previous best, or the score if it beat it.</param>
        /// <param name="isNewBest">Whether the run beat the previous best score.</param>
        /// <param name="highestTier">Index of the highest tier reached.</param>
        /// <param name="piecesDropped">Pieces dropped in the run.</param>
        /// <param name="merges">Merges in the run.</param>
        /// <param name="maxCombo">Longest combo of the run.</param>
        /// <param name="durationSeconds">Play time in seconds.</param>
        public RunSummary(int score, int bestScore, bool isNewBest, int highestTier, int piecesDropped, int merges, int maxCombo, float durationSeconds)
        {
            Score = score;
            BestScore = bestScore;
            IsNewBest = isNewBest;
            HighestTier = highestTier;
            PiecesDropped = piecesDropped;
            Merges = merges;
            MaxCombo = maxCombo;
            DurationSeconds = durationSeconds;
        }

        /// <summary>Final score of the run.</summary>
        public int Score { get; }

        /// <summary>Best score after the run: the previous best, or the final score when the run beat it.</summary>
        public int BestScore { get; }

        /// <summary>Whether the run beat the previous best score.</summary>
        public bool IsNewBest { get; }

        /// <summary>Index of the highest tier reached in the run.</summary>
        public int HighestTier { get; }

        /// <summary>Pieces dropped in the run.</summary>
        public int PiecesDropped { get; }

        /// <summary>Merges in the run.</summary>
        public int Merges { get; }

        /// <summary>Longest combo of the run.</summary>
        public int MaxCombo { get; }

        /// <summary>Play time of the run in seconds.</summary>
        public float DurationSeconds { get; }

        /// <summary>
        /// Takes the snapshot of a run.
        /// </summary>
        /// <param name="score">The score system of the run.</param>
        /// <param name="durationSeconds">Play time of the run in seconds.</param>
        public static RunSummary From(ScoreSystem score, float durationSeconds)
        {
            var best = score.IsNewBest ? score.Score : score.BestScore;
            return new RunSummary(score.Score, best, score.IsNewBest, score.HighestTierReached, score.PiecesDropped, score.Merges, score.MaxCombo, durationSeconds);
        }
    }
}
