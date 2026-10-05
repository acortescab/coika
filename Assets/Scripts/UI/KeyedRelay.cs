using System;
using Coika.Core;
using UnityEngine.Events;

namespace Coika.UI
{
    /// <summary>
    /// Connects the value event of a control (a slider's or a toggle's <c>onValueChanged</c>) to a handler that
    /// receives the setting the control stands for and the new value. One type serves every kind of control, as
    /// <see cref="ButtonRelay{T}"/> does for buttons. It caches its action, so binding and dragging allocate nothing.
    /// </summary>
    /// <typeparam name="T">The value of the control: a float for a slider, a bool for a toggle.</typeparam>
    public sealed class KeyedRelay<T> : IRelay
    {
        private readonly UnityEvent<T> _source;
        private readonly SettingKey _key;
        private readonly Action<SettingKey, T> _onChanged;
        private readonly UnityAction<T> _onValueChanged;

        /// <summary>
        /// Creates a relay. It does not listen until <see cref="Bind"/>.
        /// </summary>
        /// <param name="source">The value event of the control.</param>
        /// <param name="key">Passed to the handler.</param>
        /// <param name="onChanged">Called with the key and the new value on every change.</param>
        /// <exception cref="ArgumentNullException">The event or the handler is null.</exception>
        public KeyedRelay(UnityEvent<T> source, SettingKey key, Action<SettingKey, T> onChanged)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _key = key;
            _onValueChanged = HandleValueChanged;
        }

        /// <inheritdoc />
        public void Bind()
        {
            _source.AddListener(_onValueChanged);
        }

        /// <inheritdoc />
        public void Unbind()
        {
            _source.RemoveListener(_onValueChanged);
        }

        /// <summary>
        /// Passes the key and the value to the handler.
        /// </summary>
        /// <param name="value">The new value of the control.</param>
        private void HandleValueChanged(T value)
        {
            _onChanged(_key, value);
        }
    }
}
