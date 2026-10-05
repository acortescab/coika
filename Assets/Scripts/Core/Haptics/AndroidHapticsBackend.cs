#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// Android backend: one-shot <c>VibrationEffect</c>s with their own amplitude (API 26+, the minimum of the
    /// project). Everything is built in the constructor and Play goes through raw JNI with a prebuilt argument
    /// array, so a call allocates nothing.
    /// </summary>
    public sealed class AndroidHapticsBackend : IHapticsBackend
    {
        // Duration in milliseconds and amplitude (1-255) of each kind, indexed by the enum value.
        private static readonly long[] DURATIONS_MS = { 15, 10, 25, 40, 200 };
        private static readonly int[] AMPLITUDES = { 80, 40, 140, 255, 255 };

        private readonly AndroidJavaObject _vibrator;
        private readonly AndroidJavaObject[] _effects = new AndroidJavaObject[DURATIONS_MS.Length];
        private readonly jvalue[][] _args = new jvalue[DURATIONS_MS.Length][];
        private readonly System.IntPtr _vibrateMethod;

        /// <summary>
        /// Looks up the vibrator and builds the effects, once.
        /// </summary>
        /// <exception cref="AndroidJavaException">A platform lookup failed.</exception>
        public AndroidHapticsBackend()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            HasVibrator = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            if (!HasVibrator)
            {
                return;
            }

            using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            for (var i = 0; i < _effects.Length; i++)
            {
                _effects[i] = effectClass.CallStatic<AndroidJavaObject>("createOneShot", DURATIONS_MS[i], AMPLITUDES[i]);
                _args[i] = new[] { new jvalue { l = _effects[i].GetRawObject() } };
            }

            _vibrateMethod = AndroidJNIHelper.GetMethodID(_vibrator.GetRawClass(), "vibrate", "(Landroid/os/VibrationEffect;)V");
        }

        /// <summary>True when the device has a vibrator. Without one the backend must not be used.</summary>
        public bool HasVibrator { get; }

        /// <summary>
        /// Vibrates with the effect of the kind.
        /// </summary>
        /// <param name="kind">The effect to play.</param>
        public void Play(HapticKind kind)
        {
            AndroidJNI.CallVoidMethod(_vibrator.GetRawObject(), _vibrateMethod, _args[(int)kind]);
        }
    }
}
#endif
