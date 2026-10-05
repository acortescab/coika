using Coika.Core.Animation;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// One visual effect of a piece. It owns its clock and answers with the scale and the tint it contributes; the
    /// <see cref="PieceAnimator"/> multiplies the contributions of every active effect, so effects never know about
    /// each other. Effects are created once per animator and reused, so nothing allocates while they play.
    /// </summary>
    public abstract class PieceEffect
    {
        /// <summary>The clock of the effect, for effects that last a fixed time.</summary>
        protected TweenTimer _timer;

        /// <summary>The group of effects this one excludes when it starts.</summary>
        public abstract PieceEffectGroup Group { get; }

        /// <summary>Whether the effect is running and contributes to the scale and the tint.</summary>
        public virtual bool IsActive => _timer.IsActive;

        /// <summary>
        /// Starts the effect from its beginning.
        /// </summary>
        /// <param name="config">Tuning of the effect.</param>
        /// <param name="parameter">Effect-specific input, such as the impulse of a landing. Ignored by most.</param>
        public virtual void Start(FeedbackConfig config, float parameter)
        {
            _timer.Start(Duration(config));
        }

        /// <summary>
        /// Stops the effect at once.
        /// </summary>
        public virtual void Stop()
        {
            _timer.Stop();
        }

        /// <summary>
        /// Moves the effect forward.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last call.</param>
        public virtual void Advance(float deltaTime)
        {
            _timer.Advance(deltaTime);
        }

        /// <summary>
        /// The scale the effect contributes right now, as a factor on each axis. Only called while active.
        /// </summary>
        /// <param name="config">Tuning of the effect.</param>
        /// <param name="piece">The piece being animated, for effects that depend on its movement.</param>
        /// <returns>The factor on x and y; (1, 1) changes nothing.</returns>
        public abstract Vector2 Evaluate(FeedbackConfig config, Piece piece);

        /// <summary>
        /// The tint the effect contributes right now, multiplied with the tint of the other active effects. Only
        /// called while active. Most effects only scale, so the default changes nothing.
        /// </summary>
        /// <param name="config">Tuning of the effect.</param>
        /// <param name="piece">The piece being animated.</param>
        /// <returns>The colour factor; white changes nothing.</returns>
        public virtual Color Tint(FeedbackConfig config, Piece piece)
        {
            return Color.white;
        }

        /// <summary>
        /// Length of the effect in seconds, for effects that last a fixed time.
        /// </summary>
        /// <param name="config">Tuning of the effect.</param>
        /// <returns>The duration.</returns>
        protected virtual float Duration(FeedbackConfig config)
        {
            return 0f;
        }
    }
}
