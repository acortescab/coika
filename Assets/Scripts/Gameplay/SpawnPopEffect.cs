using Coika.Core.Animation;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// A new piece grows from nothing to full size with a little overshoot.
    /// </summary>
    public sealed class SpawnPopEffect : PieceEffect
    {
        /// <inheritdoc />
        public override PieceEffectGroup Group => PieceEffectGroup.Pop;

        /// <inheritdoc />
        public override Vector2 Evaluate(FeedbackConfig config, Piece piece)
        {
            return Vector2.one * Tween.OutBack(_timer.Progress, config.Animations.SpawnOvershoot);
        }

        /// <inheritdoc />
        protected override float Duration(FeedbackConfig config)
        {
            return config.Animations.SpawnDuration;
        }
    }
}
