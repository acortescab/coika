using System;
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// The one place that picks the platform backend (S-110), so no <c>#if</c> reaches gameplay code.
    /// </summary>
    public static class HapticsBackendFactory
    {
        /// <summary>
        /// Creates the backend of the running platform. The Editor, unsupported platforms and devices without a
        /// vibrator get the null backend silently. A failing platform lookup logs one warning and also gets it.
        /// </summary>
        /// <returns>A backend that is always usable.</returns>
        public static IHapticsBackend Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var backend = new AndroidHapticsBackend();
                return backend.HasVibrator ? backend : new NullHapticsBackend();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Haptics unavailable: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            try
            {
                return new IosHapticsBackend();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Haptics unavailable: {e.Message}");
            }
#endif
            return new NullHapticsBackend();
        }
    }
}
