using System;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// Connects one <see cref="Button"/> to a handler that receives a value, so a view with many buttons raises one
    /// event with a value instead of one event, one cached action and one handler per button. It caches its own
    /// <see cref="UnityAction"/>, so binding and unbinding allocate nothing. A view binds its relays in
    /// <c>OnEnable</c> and unbinds them in <c>OnDisable</c> (S-23).
    /// </summary>
    /// <typeparam name="T">The value the button stands for.</typeparam>
    public sealed class ButtonRelay<T> : IRelay
    {
        private readonly Button _button;
        private readonly T _value;
        private readonly Action<T> _onClicked;
        private readonly UnityAction _onClick;

        /// <summary>
        /// Creates a relay. It does not listen until <see cref="Bind"/>.
        /// </summary>
        /// <param name="button">The button to listen to.</param>
        /// <param name="value">Passed to the handler on every click.</param>
        /// <param name="onClicked">Called with the value on every click.</param>
        /// <exception cref="ArgumentNullException">The button or the handler is null.</exception>
        public ButtonRelay(Button button, T value, Action<T> onClicked)
        {
            _button = button != null ? button : throw new ArgumentNullException(nameof(button));
            _onClicked = onClicked ?? throw new ArgumentNullException(nameof(onClicked));
            _value = value;
            _onClick = HandleClick;
        }

        /// <inheritdoc />
        public void Bind()
        {
            _button.onClick.AddListener(_onClick);
        }

        /// <inheritdoc />
        public void Unbind()
        {
            _button.onClick.RemoveListener(_onClick);
        }

        /// <summary>
        /// Passes the value to the handler.
        /// </summary>
        private void HandleClick()
        {
            _onClicked(_value);
        }
    }
}
