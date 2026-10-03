using System;
using System.Collections.Generic;
using Coika.Core;
using Coika.Data;

namespace Coika.Gameplay
{
    /// <summary>
    /// What the UI needs to show a run, handed out by the composition root when the run starts. It only carries
    /// references: the owner keeps ownership, and the asset service stays the owner's to dispose.
    /// </summary>
    public sealed class RunContext
    {
        /// <summary>
        /// Creates the context of a run.
        /// </summary>
        /// <param name="score">Score and combo of the run.</param>
        /// <param name="queue">Spawn queue of the run, for the next-piece preview.</param>
        /// <param name="tiers">Every tier, in tier order.</param>
        /// <param name="assets">Asset service to load the tier sprites with.</param>
        /// <exception cref="ArgumentNullException">A value is null.</exception>
        public RunContext(ScoreSystem score, SpawnQueue queue, IReadOnlyList<TierDefinition> tiers, IAssetService assets)
        {
            Score = score ?? throw new ArgumentNullException(nameof(score));
            Queue = queue ?? throw new ArgumentNullException(nameof(queue));
            Tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        /// <summary>Score and combo of the run.</summary>
        public ScoreSystem Score { get; }

        /// <summary>Spawn queue of the run.</summary>
        public SpawnQueue Queue { get; }

        /// <summary>Every tier, in tier order.</summary>
        public IReadOnlyList<TierDefinition> Tiers { get; }

        /// <summary>Asset service of the owner; loads made with it are released by whoever made them.</summary>
        public IAssetService Assets { get; }
    }
}
