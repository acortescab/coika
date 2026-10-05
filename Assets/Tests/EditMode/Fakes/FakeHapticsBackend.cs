using System;
using System.Collections.Generic;
using Coika.Core;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Records the haptics sent to the device and can be told to fail, so tests need no platform.
    /// </summary>
    public sealed class FakeHapticsBackend : IHapticsBackend
    {
        /// <summary>The effects played, in order.</summary>
        public List<HapticKind> Played { get; } = new List<HapticKind>(16);

        /// <summary>Number of Play calls, including the ones that threw.</summary>
        public int Calls { get; private set; }

        /// <summary>When true, Play throws instead of recording.</summary>
        public bool Throws { get; set; }

        /// <summary>
        /// Records the effect, or throws when <see cref="Throws"/> is set.
        /// </summary>
        /// <param name="kind">The effect to play.</param>
        /// <exception cref="InvalidOperationException">The fake was told to fail.</exception>
        public void Play(HapticKind kind)
        {
            Calls++;
            if (Throws)
            {
                throw new InvalidOperationException("fake failure");
            }

            if (Played.Count < Played.Capacity)
            {
                Played.Add(kind);
            }
        }
    }
}
