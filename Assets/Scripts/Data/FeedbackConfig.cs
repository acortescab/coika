using UnityEngine;

namespace Coika.Data
{
    /// <summary>
    /// Tuning of the feedback of the game events (GDD §9), split in one group per domain so each stays small: the
    /// piece animations, the particles, the screen effects and the sounds with the haptics. Immutable data at
    /// runtime (S-31, S-30). A new tunable value goes in the group it belongs to, not here.
    /// </summary>
    [CreateAssetMenu(fileName = "FeedbackConfig", menuName = "Scriptable Objects/FeedbackConfig")]
    public class FeedbackConfig : ScriptableObject
    {
        /// <summary>Pixels per world unit that the camera shake is snapped to (the Pixel Perfect Camera reference).</summary>
        public const float SHAKE_SNAP_UNITS_PER_STEP = 1f / 16f;

        /// <summary>Capacity of the shake slots: when all are used the one that ends first is replaced.</summary>
        public const int SHAKE_SLOTS = 8;

        [SerializeField]
        private PieceAnimationSettings _animations = new();
        [SerializeField]
        private ParticleSettings _particles = new();
        [SerializeField]
        private ScreenFxSettings _screenFx = new();
        [SerializeField]
        private FeedbackSoundSettings _sound = new();

        /// <summary>The spawn pop, the drop stretch, the landing squash, the merge pop and the game-over flash.</summary>
        public PieceAnimationSettings Animations => _animations;

        /// <summary>The pooled particles and rings.</summary>
        public ParticleSettings Particles => _particles;

        /// <summary>The shake, the slow-mo and the screen flash.</summary>
        public ScreenFxSettings ScreenFx => _screenFx;

        /// <summary>The sounds and haptics of the game events.</summary>
        public FeedbackSoundSettings Sound => _sound;
    }
}
