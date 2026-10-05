using System;
using System.Collections.Generic;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Turns merges, drops and the Supernova into the score of the run (GDD §4) and keeps the stats of the Game Over
    /// screen. A merge is worth the merge score of the new tier times the combo multiplier, floored; a drop is worth
    /// the tier index, and the Supernova the bonus of the config, both without the multiplier. It shows nothing and
    /// saves nothing: <see cref="ScoreChanged"/> and <see cref="NewBestReached"/> are for the HUD and the save system.
    /// <para>
    /// The scoring is in three public methods (<see cref="OnMerged"/>, <see cref="OnPieceDropped"/> and
    /// <see cref="OnSupernova"/>), so it is tested without the physics. <see cref="Bind"/> only connects them to the
    /// events of the <see cref="MergeSystem"/> and the <see cref="DropController"/>, and <see cref="Unbind"/> undoes
    /// it. The composition root (issue #11) creates the system, binds it, and calls <see cref="Tick"/> every frame so
    /// a combo that was not continued ends.
    /// </para>
    /// <para>
    /// The merge system raises its events in FixedUpdate, where <c>Time.timeAsDouble</c> is the fixed time, while
    /// Update sees the frame time, which can be up to one physics step ahead. Calling <see cref="Tick"/> from Update
    /// can therefore end a combo up to a step early at the edge of the window; calling it from FixedUpdate, like the
    /// merges, keeps both on the same clock.
    /// </para>
    /// <para>
    /// The score never wraps or goes negative: it stops at <see cref="int.MaxValue"/>. Handling an event allocates
    /// nothing (S-50).
    /// </para>
    /// </summary>
    public sealed class ScoreSystem
    {
        private readonly IReadOnlyList<TierDefinition> _tiers;
        private readonly Func<double> _clock;
        private readonly int _supernovaBonus;
        private readonly Action<int, Vector2, Vector2> _onMerged;
        private readonly Action<Vector2> _onSupernova;
        private readonly Action<int> _onPieceDropped;

        private MergeSystem _mergeSystem;
        private DropController _dropController;
        private bool _bound;
        private bool _bestWasReset;

        /// <summary>
        /// Creates a system for a run: score 0 and no combo. It copies the combo values and the Supernova bonus of the
        /// config, so the config can change afterwards.
        /// </summary>
        /// <param name="config">Source of the combo values and the Supernova bonus.</param>
        /// <param name="tiers">Every tier, in tier order, to find the merge score of a tier.</param>
        /// <param name="clock">Gives the current time in seconds; <c>() =&gt; Time.timeAsDouble</c> in the game.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public ScoreSystem(GameConfig config, IReadOnlyList<TierDefinition> tiers, Func<double> clock)
        {
            var source = config != null ? config : throw new ArgumentNullException(nameof(config));
            _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _supernovaBonus = source.SuperNovaBonus;
            ComboTracker = new ComboTracker(source);

            _onMerged = HandleMerged;
            _onSupernova = HandleSupernova;
            _onPieceDropped = OnPieceDropped;
        }

        /// <summary>Raised with the new score and the points just added, only when the score changed.</summary>
        public event Action<int, int> ScoreChanged;

        /// <summary>Raised once per run, the first time the score goes above <see cref="BestScore"/>.</summary>
        public event Action NewBestReached;

        /// <summary>The combo of the run. Subscribe to its <see cref="ComboTracker.ComboChanged"/> to show it.</summary>
        public ComboTracker ComboTracker { get; }

        /// <summary>Score of the run. Never negative, and it stops at <see cref="int.MaxValue"/>.</summary>
        public int Score { get; private set; }

        /// <summary>
        /// Best score to beat. The save system sets it when it loads; it is 0 until then. It is kept by
        /// <see cref="ResetForNewRun"/>.
        /// </summary>
        public int BestScore { get; set; }

        /// <summary>Whether the score went above <see cref="BestScore"/> in this run.</summary>
        public bool IsNewBest { get; private set; }

        /// <summary>
        /// Erases the best score while a run is in progress (Reset progress). The run is not a new best for it: the
        /// points scored so far, and the ones it scores from now on, raise <see cref="BestScore"/> quietly, so
        /// <see cref="IsNewBest"/> stays false and <see cref="NewBestReached"/> is not raised until the next run.
        /// </summary>
        public void ResetBest()
        {
            BestScore = Score;
            IsNewBest = false;
            _bestWasReset = true;
        }

        /// <summary>
        /// Highest tier index of a piece that was dropped or created by a merge in this run: 0 before the first
        /// piece.
        /// </summary>
        public int HighestTierReached { get; private set; }

        /// <summary>Merges in this run.</summary>
        public int Merges { get; private set; }

        /// <summary>Pieces dropped in this run.</summary>
        public int PiecesDropped { get; private set; }

        /// <summary>Longest combo of this run.</summary>
        public int MaxCombo { get; private set; }

        /// <summary>
        /// Starts listening to the merges, the Supernova and the drops. Calling it again binds to the new sources.
        /// </summary>
        /// <param name="mergeSystem">Source of <see cref="MergeSystem.Merged"/> and the Supernova.</param>
        /// <param name="dropController">Source of <see cref="DropController.PieceDropped"/>.</param>
        /// <exception cref="ArgumentNullException">A source is null.</exception>
        public void Bind(MergeSystem mergeSystem, DropController dropController)
        {
            var merge = mergeSystem != null ? mergeSystem : throw new ArgumentNullException(nameof(mergeSystem));
            var drop = dropController != null ? dropController : throw new ArgumentNullException(nameof(dropController));

            Unbind();
            _mergeSystem = merge;
            _dropController = drop;
            _mergeSystem.Merged += _onMerged;
            _mergeSystem.SupernovaTriggered += _onSupernova;
            _dropController.PieceDropped += _onPieceDropped;
            _bound = true;
        }

        /// <summary>
        /// Stops listening. Safe to call when it is not bound, and after the sources were destroyed.
        /// </summary>
        public void Unbind()
        {
            if (!_bound)
            {
                return;
            }

            _bound = false;
            _mergeSystem.Merged -= _onMerged;
            _mergeSystem.SupernovaTriggered -= _onSupernova;
            _dropController.PieceDropped -= _onPieceDropped;
            _mergeSystem = null;
            _dropController = null;
        }

        /// <summary>
        /// Scores a merge: registers it in the combo first, so it counts toward its own multiplier, then adds the
        /// merge score of the new tier times that multiplier, floored.
        /// </summary>
        /// <param name="tier">Index of the tier the merge created.</param>
        /// <exception cref="ArgumentOutOfRangeException">The tier is not one of the tiers.</exception>
        public void OnMerged(int tier)
        {
            var definition = GetTier(tier);
            ComboTracker.RegisterMerge(_clock());

            Merges++;
            MaxCombo = Math.Max(MaxCombo, ComboTracker.Combo);
            HighestTierReached = Math.Max(HighestTierReached, tier);
            AddScore(FloorPoints(definition.MergeScore, ComboTracker.Multiplier));
        }

        /// <summary>
        /// Scores a drop: the tier index, with no combo multiplier.
        /// </summary>
        /// <param name="tier">Index of the tier of the dropped piece.</param>
        public void OnPieceDropped(int tier)
        {
            PiecesDropped++;
            HighestTierReached = Math.Max(HighestTierReached, tier);
            AddScore(tier);
        }

        /// <summary>
        /// Scores the Supernova: the bonus of the config, with no combo multiplier.
        /// </summary>
        public void OnSupernova()
        {
            AddScore(_supernovaBonus);
        }

        /// <summary>
        /// Ends a combo that was not continued within its window. The owner calls it every frame.
        /// </summary>
        public void Tick()
        {
            ComboTracker.Tick(_clock());
        }

        /// <summary>
        /// The points of a merge: the merge score of the tier times the multiplier, floored. It is the only place
        /// with the formula, so the HUD can use it for a preview.
        /// </summary>
        /// <param name="tier">Index of the tier the merge creates.</param>
        /// <param name="multiplier">The combo multiplier.</param>
        /// <returns>The points, from 0 to <see cref="int.MaxValue"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The tier is not one of the tiers.</exception>
        public int CalculateMergeScore(int tier, float multiplier)
        {
            return FloorPoints(GetTier(tier).MergeScore, multiplier);
        }

        /// <summary>
        /// Starts a new run: score, combo and stats go back to 0 and <see cref="IsNewBest"/> to false. The best score
        /// and the binding stay, and no event is raised: whoever shows the score reads it when the run starts.
        /// </summary>
        public void ResetForNewRun()
        {
            Score = 0;
            IsNewBest = false;
            _bestWasReset = false;
            HighestTierReached = 0;
            Merges = 0;
            PiecesDropped = 0;
            MaxCombo = 0;
            ComboTracker.Reset();
        }

        /// <summary>
        /// The definition of a tier, checked before any state changes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The tier is not one of the tiers.</exception>
        private TierDefinition GetTier(int tier)
        {
            if (tier < 0 || tier >= _tiers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "The tier does not exist.");
            }

            return _tiers[tier];
        }

        /// <summary>
        /// A merge score times a multiplier, floored (GDD §4) and kept between 0 and <see cref="int.MaxValue"/>.
        /// Computed in double, so a large score or multiplier cannot lose the exact floor.
        /// </summary>
        private static int FloorPoints(float mergeScore, float multiplier)
        {
            var points = Math.Floor(mergeScore * (double)multiplier);
            return points <= 0d ? 0 : points >= int.MaxValue ? int.MaxValue : (int)points;
        }

        /// <summary>
        /// Adapts the three-argument event of the merge system to <see cref="OnMerged"/>.
        /// </summary>
        private void HandleMerged(int tier, Vector2 position, Vector2 velocity)
        {
            OnMerged(tier);
        }

        /// <summary>
        /// Adapts the position-carrying event of the merge system to <see cref="OnSupernova"/>.
        /// </summary>
        private void HandleSupernova(Vector2 position)
        {
            OnSupernova();
        }

        /// <summary>
        /// Adds points to the score, stopping at <see cref="int.MaxValue"/>, and raises the events. Nothing happens
        /// for zero or negative points, or when the score is already at the top.
        /// </summary>
        private void AddScore(int points)
        {
            if (points <= 0)
            {
                return;
            }

            var total = Math.Min((long)Score + points, int.MaxValue);
            var delta = (int)(total - Score);
            if (delta == 0)
            {
                return;
            }

            Score = (int)total;
            ScoreChanged?.Invoke(Score, delta);

            if (_bestWasReset)
            {
                BestScore = Math.Max(BestScore, Score);
            }
            else if (!IsNewBest && Score > BestScore)
            {
                IsNewBest = true;
                NewBestReached?.Invoke();
            }
        }
    }
}
