using System;
using Coika.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Coika.UI
{
    /// <summary>
    /// Connects the release of a <see cref="Slider"/> to a handler that receives the setting it stands for. A
    /// <c>Slider</c> has no such event, so the relay adds a <see cref="SliderReleaseNotifier"/> to it when it has
    /// none. It caches its action, so binding allocates nothing.
    /// </summary>
    public sealed class ReleaseRelay : IRelay
    {
        private readonly SliderReleaseNotifier _notifier;
        private readonly SettingKey _key;
        private readonly Action<SettingKey> _onReleased;
        private readonly Action _onRelease;

        /// <summary>
        /// Creates a relay. It does not listen until <see cref="Bind"/>.
        /// </summary>
        /// <param name="slider">The slider to listen to.</param>
        /// <param name="key">Passed to the handler.</param>
        /// <param name="onReleased">Called with the key when the pointer is lifted.</param>
        /// <exception cref="ArgumentNullException">The slider or the handler is null.</exception>
        public ReleaseRelay(Slider slider, SettingKey key, Action<SettingKey> onReleased)
        {
            if (slider == null)
            {
                throw new ArgumentNullException(nameof(slider));
            }

            _onReleased = onReleased ?? throw new ArgumentNullException(nameof(onReleased));
            _key = key;
            _notifier = slider.GetComponent<SliderReleaseNotifier>();
            if (_notifier == null)
            {
                _notifier = slider.gameObject.AddComponent<SliderReleaseNotifier>();
            }

            _onRelease = HandleRelease;
        }

        /// <inheritdoc />
        public void Bind()
        {
            _notifier.Released += _onRelease;
        }

        /// <inheritdoc />
        public void Unbind()
        {
            _notifier.Released -= _onRelease;
        }

        /// <summary>
        /// Passes the key to the handler.
        /// </summary>
        private void HandleRelease()
        {
            _onReleased(_key);
        }
    }
}
