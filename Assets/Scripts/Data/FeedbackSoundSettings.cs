using System;
using Coika.Core;
using UnityEngine;

namespace Coika.Data
{
    /// <summary>
    /// Tuning of the sounds and haptics that go with the game events (GDD §9): the Heavy haptic tier, the landing
    /// thud, the drop whoosh and the danger tick. A group of <see cref="FeedbackConfig"/>.
    /// </summary>
    [Serializable]
    public class FeedbackSoundSettings
    {
        [SerializeField, Min(0)]
        private int _heavyMergeHapticMinTier = 7;
        [SerializeField, Min(0.01f)]
        private float _landVolumePerImpulse = 0.1f;
        [SerializeField, Range(0f, 1f)]
        private float _landVolumeMin = 0.15f;
        [SerializeField, Min(0.01f)]
        private float _landPitchReferenceRadius = 0.5f;
        [SerializeField, Range(0.1f, 1f)]
        private float _landPitchMin = 0.6f;
        [SerializeField, Range(1f, 3f)]
        private float _landPitchMax = 1.6f;
        [SerializeField, Range(0.1f, 2f)]
        private float _dropPitch = 0.8f;
        [SerializeField, Min(0.1f)]
        private float _dangerTickHz = 4f;
        [SerializeField, Min(0.1f)]
        private float _dangerTickSoftHz = 2f;

        /// <summary>Lowest tier index whose merge buzzes with a Heavy haptic instead of a Medium one (GDD §9).</summary>
        public int HeavyMergeHapticMinTier => _heavyMergeHapticMinTier;

        /// <summary>Landing sound volume added per unit of landing impulse, before the clamp.</summary>
        public float LandVolumePerImpulse => _landVolumePerImpulse;

        /// <summary>Quietest landing sound volume.</summary>
        public float LandVolumeMin => _landVolumeMin;

        /// <summary>Piece radius in world units at which the landing sound has its natural pitch.</summary>
        public float LandPitchReferenceRadius => _landPitchReferenceRadius;

        /// <summary>Lowest pitch of the landing sound, for the biggest pieces.</summary>
        public float LandPitchMin => _landPitchMin;

        /// <summary>Highest pitch of the landing sound, for the smallest pieces.</summary>
        public float LandPitchMax => _landPitchMax;

        /// <summary>Pitch of the drop whoosh, low so it sounds heavy.</summary>
        public float DropPitch => _dropPitch;

        /// <summary>Ticks per second of the danger sound.</summary>
        public float DangerTickHz => _dangerTickHz;

        /// <summary>Ticks per second of the danger sound while Reduce Shake is on, matching the softer pulse.</summary>
        public float DangerTickSoftHz => _dangerTickSoftHz;

        /// <summary>
        /// Chooses the haptic of a merge: Medium, or Heavy from <see cref="HeavyMergeHapticMinTier"/> up.
        /// </summary>
        /// <param name="tier">Tier index of the piece the merge created.</param>
        /// <returns>The haptic kind.</returns>
        public HapticKind MergeHaptic(int tier)
        {
            return tier >= _heavyMergeHapticMinTier ? HapticKind.Heavy : HapticKind.Medium;
        }

        /// <summary>
        /// Computes the landing sound volume, proportional to the impulse and clamped between the minimum and 1.
        /// </summary>
        /// <param name="impulse">Total normal impulse of the landing contact.</param>
        /// <returns>The volume, from 0 to 1.</returns>
        public float LandVolume(float impulse)
        {
            return Mathf.Clamp(impulse * _landVolumePerImpulse, _landVolumeMin, 1f);
        }

        /// <summary>
        /// Computes the landing sound pitch, inversely proportional to the size of the piece and clamped.
        /// </summary>
        /// <param name="radius">Radius of the piece in world units.</param>
        /// <returns>The pitch multiplier.</returns>
        public float LandPitch(float radius)
        {
            return Mathf.Clamp(_landPitchReferenceRadius / Mathf.Max(radius, 0.0001f), _landPitchMin, _landPitchMax);
        }
    }
}
