using Coika.Core.Animation;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// A piece that lands hard squashes (wider and flatter) and then stretches back a little before it rests. The
    /// amplitude follows the impulse of the landing and is clamped by the config; a landing below the config
    /// threshold plays nothing.
    /// </summary>
    public sealed class LandSquashEffect : PieceEffect
    {
        /// <summary>Fraction of the effect spent squashing; the rest is the rebound.</summary>
        private const float SQUASH_PHASE = 0.5f;

        private float _amplitude;

        /// <inheritdoc />
        public override PieceEffectGroup Group => PieceEffectGroup.Contact;

        /// <inheritdoc />
        public override void Start(FeedbackConfig config, float parameter)
        {
            _amplitude = config.LandAmplitude(parameter);
            if (_amplitude > 0f)
            {
                base.Start(config, parameter);
            }
        }

        /// <inheritdoc />
        public override Vector2 Evaluate(FeedbackConfig config, Piece piece)
        {
            var t = _timer.Progress;
            var squash = t < SQUASH_PHASE
                ? _amplitude * Tween.Bump(t / SQUASH_PHASE)
                : -config.LandReboundRatio * _amplitude * Tween.Bump((t - SQUASH_PHASE) / (1f - SQUASH_PHASE));
            return new Vector2(1f + squash, 1f - squash);
        }

        /// <inheritdoc />
        protected override float Duration(FeedbackConfig config)
        {
            return config.LandDuration;
        }
    }
}
