using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// A falling piece is stretched vertically in proportion to its fall speed. It has no fixed duration: its timer
    /// never ends, so it runs until another effect of its group (the landing) replaces it.
    /// </summary>
    public sealed class DropStretchEffect : PieceEffect
    {
        /// <summary>Share of the stretch that the width gives back, so the piece keeps roughly its area.</summary>
        private const float WIDTH_COMPENSATION = 0.5f;

        /// <inheritdoc />
        public override PieceEffectGroup Group => PieceEffectGroup.Contact;

        /// <inheritdoc />
        public override void Start(FeedbackConfig config, float parameter)
        {
            _timer.Start(float.PositiveInfinity);
        }

        /// <inheritdoc />
        public override Vector2 Evaluate(FeedbackConfig config, Piece piece)
        {
            var fall = -piece.Rigidbody.linearVelocity.y;
            var stretch = fall > 0f ? config.DropStretch * Mathf.Min(1f, fall / config.DropStretchFullSpeed) : 0f;
            return new Vector2(1f - stretch * WIDTH_COMPENSATION, 1f + stretch);
        }
    }
}
