#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// iOS backend: <c>UIImpactFeedbackGenerator</c> through the small plugin in <c>Plugins/iOS/CoikaHaptics.mm</c>.
    /// The Long kind uses <see cref="Handheld.Vibrate"/>, the only long vibration iOS offers without CoreHaptics.
    /// </summary>
    public sealed class IosHapticsBackend : IHapticsBackend
    {
        private const int STYLE_LIGHT = 0;
        private const int STYLE_MEDIUM = 1;
        private const int STYLE_HEAVY = 2;

        /// <summary>
        /// Prepares the impact generators, once.
        /// </summary>
        public IosHapticsBackend()
        {
            CoikaHapticsPrepare();
        }

        /// <summary>
        /// Plays the impact of the kind.
        /// </summary>
        /// <param name="kind">The effect to play.</param>
        public void Play(HapticKind kind)
        {
            switch (kind)
            {
                case HapticKind.VeryLight:
                    CoikaHapticsImpact(STYLE_LIGHT, 0.5f);
                    break;
                case HapticKind.Light:
                    CoikaHapticsImpact(STYLE_LIGHT, 1f);
                    break;
                case HapticKind.Medium:
                    CoikaHapticsImpact(STYLE_MEDIUM, 1f);
                    break;
                case HapticKind.Heavy:
                    CoikaHapticsImpact(STYLE_HEAVY, 1f);
                    break;
                default:
                    Handheld.Vibrate();
                    break;
            }
        }

        /// <summary>Creates and prepares the three impact generators.</summary>
        [DllImport("__Internal")]
        private static extern void CoikaHapticsPrepare();

        /// <summary>Fires an impact of the style (0 light, 1 medium, 2 heavy) with the intensity (0-1).</summary>
        [DllImport("__Internal")]
        private static extern void CoikaHapticsImpact(int style, float intensity);
    }
}
#endif
