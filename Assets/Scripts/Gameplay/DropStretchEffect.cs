using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// A falling piece is stretched vertically in proportion to its fall speed. It has no duration: it runs until
    /// another effect of its group (the landing) replaces it.
    /// </summary>
    public sealed class DropStretchEffect : PieceEffect
    {
        private bool _active;

        /// <inheritdoc />
        public override PieceEffectGroup Group => PieceEffectGroup.Contact;

        /// <inheritdoc />
        public override bool IsActive => _active;

        /// <inheritdoc />
        public override void Start(FeedbackConfig config, float parameter)
        {
            _active = true;
        }

        /// <inheritdoc />
        public override void Stop()
        {
            _active = false;
        }

        /// <inheritdoc />
        public override void Advance(float deltaTime)
        {
        }

        /// <inheritdoc />
        public override Vector2 Evaluate(FeedbackConfig config, Piece piece)
        {
            var fall = -piece.Rigidbody.linearVelocity.y;
            var stretch = fall > 0f ? config.DropStretch * Mathf.Min(1f, fall / config.DropStretchFullSpeed) : 0f;
            return new Vector2(1f - stretch * 0.5f, 1f + stretch);
        }
    }
}
