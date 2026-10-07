using System.Collections.Generic;
using Coika.Fx;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// A time scale that only remembers: it never touches <c>Time.timeScale</c>, and it records every value written, so a
    /// test can see what the <see cref="TimeScaleOwner"/> asked for.
    /// </summary>
    public sealed class FakeTimeScale : ITimeScale
    {
        private float _value = 1f;

        /// <summary>Every value written, in order, without the initial 1.</summary>
        public List<float> Writes { get; } = new();

        /// <inheritdoc />
        public float Value
        {
            get => _value;
            set
            {
                _value = value;
                Writes.Add(value);
            }
        }
    }
}
