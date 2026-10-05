using System.Collections.Generic;
using Coika.Core;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Records the haptics a consumer asks for, for tests of the code that will map game events to haptics.
    /// </summary>
    public sealed class FakeHaptics : IHaptics
    {
        /// <summary>The effects requested, in order.</summary>
        public List<HapticKind> Played { get; } = new List<HapticKind>();

        /// <summary>
        /// Records the effect.
        /// </summary>
        /// <param name="kind">The effect requested.</param>
        public void Play(HapticKind kind)
        {
            Played.Add(kind);
        }
    }
}
