using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Purely visual animation of one piece (GDD §9). It lives on the sprite child of the Piece prefab and only
    /// ever changes that child's local scale and the colour of its sprite, so the body and the collider on the root
    /// never change and the physics stay deterministic. Each animation is a <see cref="PieceEffect"/>; the animator
    /// advances the active ones and multiplies what they contribute, so a new animation is a new effect class and
    /// nothing here changes. At rest the child is set back to an exact identity scale, a zero offset and a white
    /// tint, which keeps the sprite free of sub-pixel shimmer.
    /// <para>
    /// The <see cref="Piece"/> starts the effects with <see cref="Play"/> and <see cref="Update"/> advances them with
    /// <see cref="Tick"/>, which tests call directly with exact time steps. Nothing here allocates once the effects
    /// exist.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceAnimator : MonoBehaviour
    {
        private FeedbackConfig _config;
        private Piece _piece;
        private PieceEffect[] _effects;
        private SpriteRenderer _renderer;

        /// <summary>Whether any effect is still running, so the child is not at rest.</summary>
        public bool IsRunning
        {
            get
            {
                var effects = Effects;
                for (var i = 0; i < effects.Length; i++)
                {
                    if (effects[i].IsActive)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Current local scale of the visual child.</summary>
        public Vector3 VisualScale => transform.localScale;

        /// <summary>The effects, indexed by <see cref="PieceEffectId"/>. Created on first use.</summary>
        private PieceEffect[] Effects => _effects ??= CreateEffects();

        /// <summary>
        /// Binds the animator to its piece and tuning, and puts the visual back at rest. A null config disables every
        /// effect. The piece calls it each time it is initialized, so a pooled piece never keeps old state.
        /// </summary>
        /// <param name="piece">The piece this animator belongs to.</param>
        /// <param name="config">Tuning of the effects. May be null.</param>
        public void Begin(Piece piece, FeedbackConfig config)
        {
            _piece = piece;
            _config = config;
            ResetState();
        }

        /// <summary>
        /// Stops every effect and sets the visual to identity scale, zero offset and a white tint.
        /// </summary>
        public void ResetState()
        {
            var effects = Effects;
            for (var i = 0; i < effects.Length; i++)
            {
                effects[i].Stop();
            }

            ApplyRest();
        }

        /// <summary>
        /// Starts an effect and stops the running effects of its group. Does nothing without a config.
        /// </summary>
        /// <param name="id">The effect to start.</param>
        /// <param name="parameter">Effect-specific input, such as the impulse of a landing.</param>
        public void Play(PieceEffectId id, float parameter = 0f)
        {
            if (_config == null)
            {
                return;
            }

            var effects = Effects;
            var effect = effects[(int)id];
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i].Group == effect.Group)
                {
                    effects[i].Stop();
                }
            }

            effect.Start(_config, parameter);
            Apply();
        }

        /// <summary>
        /// Advances the running effects and applies the result to the visual child. Does nothing at rest.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last call.</param>
        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            var effects = Effects;
            for (var i = 0; i < effects.Length; i++)
            {
                effects[i].Advance(deltaTime);
            }

            Apply();
        }

        /// <summary>
        /// Advances the effects with the frame time.
        /// </summary>
        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Builds one instance of every effect, in the order of <see cref="PieceEffectId"/>.
        /// </summary>
        /// <returns>The effects.</returns>
        private static PieceEffect[] CreateEffects()
        {
            return new PieceEffect[]
            {
                new SpawnPopEffect(),
                new MergePopEffect(),
                new DropStretchEffect(),
                new LandSquashEffect(),
                new GameOverFlashEffect(),
            };
        }

        /// <summary>
        /// Multiplies the scale and the tint of every active effect and applies them to the visual. When no effect is left running
        /// it goes back to the exact rest state.
        /// </summary>
        private void Apply()
        {
            var scale = Vector2.one;
            var tint = Color.white;
            var running = false;
            var effects = Effects;
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i].IsActive)
                {
                    running = true;
                    scale.Scale(effects[i].Evaluate(_config, _piece));
                    tint *= effects[i].Tint(_config, _piece);
                }
            }

            if (!running)
            {
                ApplyRest();
                return;
            }

            transform.localScale = new Vector3(scale.x, scale.y, 1f);
            transform.localPosition = Vector3.zero;
            SetTint(tint);
        }

        /// <summary>
        /// Tints the sprite of the visual child. Does nothing when the child has no sprite renderer.
        /// </summary>
        /// <param name="tint">The colour to apply.</param>
        private void SetTint(Color tint)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_renderer != null)
            {
                _renderer.color = tint;
            }
        }

        /// <summary>
        /// Puts the visual child at exact identity scale, zero offset and white tint: the state in which the sprite is at rest.
        /// </summary>
        private void ApplyRest()
        {
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;
            SetTint(Color.white);
        }
    }
}
