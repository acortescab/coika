using System.Collections.Generic;
using Coika.Fx;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Records every request to the shake, the slow-mo and the flash, and never touches the camera or the time scale.
    /// </summary>
    public sealed class FakeScreenEffects : IScreenShake, ISlowMo, IScreenFlash
    {
        /// <summary>The shakes asked for.</summary>
        public List<ScreenRequest> Shakes { get; } = new();

        /// <summary>The slow-mos asked for; the amplitude is the scale.</summary>
        public List<ScreenRequest> SlowMos { get; } = new();

        /// <summary>The durations of the flashes asked for.</summary>
        public List<float> Flashes { get; } = new();

        /// <summary>How many times the shake or the flash was cleared.</summary>
        public int ClearCalls { get; private set; }

        /// <summary>How many times the slow-mo was cancelled.</summary>
        public int CancelCalls { get; private set; }

        /// <inheritdoc />
        public void Shake(float amplitude, float duration)
        {
            Shakes.Add(new ScreenRequest(amplitude, duration));
        }

        /// <inheritdoc />
        public void Clear()
        {
            ClearCalls++;
        }

        /// <inheritdoc />
        public void Begin(float scale, float duration)
        {
            SlowMos.Add(new ScreenRequest(scale, duration));
        }

        /// <inheritdoc />
        public void Cancel()
        {
            CancelCalls++;
        }

        /// <inheritdoc />
        public void Flash(float duration)
        {
            Flashes.Add(duration);
        }

        /// <summary>
        /// Sets the counts of clears and cancels back to zero.
        /// </summary>
        public void ResetCounts()
        {
            ClearCalls = 0;
            CancelCalls = 0;
        }

        /// <summary>
        /// Forgets the requests, keeping the counts of clears and cancels.
        /// </summary>
        public void Reset()
        {
            Shakes.Clear();
            SlowMos.Clear();
            Flashes.Clear();
        }
    }
}
