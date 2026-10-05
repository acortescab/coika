using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Coika.UI
{
    /// <summary>
    /// Raises <see cref="Released"/> when a pointer is lifted from the slider it sits on. A <c>Slider</c> has no
    /// such event, and the SFX volume preview plays on release instead of on every step of the drag.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliderReleaseNotifier : MonoBehaviour, IPointerUpHandler
    {
        /// <summary>Raised when the pointer that was on the slider is lifted.</summary>
        public event Action Released;

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            Released?.Invoke();
        }
    }
}
