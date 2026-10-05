using Coika.Core.Animation;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The flash of a piece at game over: after a delay its sprite is tinted towards the flash colour and back. The
    /// director gives each piece a delay by height, so the flash sweeps from the top of the jar to the bottom. It
    /// does not change the scale.
    /// </summary>
    public sealed class GameOverFlashEffect : PieceEffect
    {
        private float _delay;
        private float _flash;

        /// <inheritdoc />
        public override PieceEffectGroup Group => PieceEffectGroup.Flash;

        /// <inheritdoc />
        public override void Start(FeedbackConfig config, float parameter)
        {
            _delay = Mathf.Max(0f, parameter);
            _flash = Mathf.Max(0.0001f, config.Animations.GameOverFlashDuration);
            _timer.Start(_delay + _flash);
        }

        /// <inheritdoc />
        public override Vector2 Evaluate(FeedbackConfig config, Piece piece)
        {
            return Vector2.one;
        }

        /// <inheritdoc />
        public override Color Tint(FeedbackConfig config, Piece piece)
        {
            var elapsed = _timer.Progress * (_delay + _flash);
            var t = (elapsed - _delay) / _flash;
            return Color.Lerp(Color.white, config.Animations.GameOverFlashColor, Tween.Bump(t));
        }
    }
}
