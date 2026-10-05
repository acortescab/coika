using Coika.Core.Animation;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// A piece created by a merge pops from nothing up to a peak and settles back at full size.
    /// </summary>
    public sealed class MergePopEffect : PieceEffect
    {
        /// <inheritdoc />
        public override PieceEffectGroup Group => PieceEffectGroup.Pop;

        /// <inheritdoc />
        public override Vector2 Evaluate(FeedbackConfig config, Piece piece)
        {
            return Vector2.one * Tween.PopThrough(Timer.Progress, config.MergePopPeak, config.MergePopPeakAt);
        }

        /// <inheritdoc />
        protected override float Duration(FeedbackConfig config)
        {
            return config.MergePopDuration;
        }
    }
}
