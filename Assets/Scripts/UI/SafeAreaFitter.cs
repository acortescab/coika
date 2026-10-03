using UnityEngine;

namespace Coika.UI
{
    /// <summary>
    /// Fits a RectTransform to <see cref="Screen.safeArea"/> by setting its anchors, so the HUD stays clear of
    /// notches and system bars (S-92). Put it on a full-screen child of the canvas and parent the HUD to it. It
    /// re-applies only when the safe area or the screen size changes, and allocates nothing.
    /// </summary>
    [AddComponentMenu("Coika/UI/Safe Area Fitter")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _appliedArea;
        private Vector2Int _appliedSize;

        /// <summary>
        /// Converts a safe area in pixels into normalized anchors. A screen with no pixels, or an empty area, gives
        /// the full screen, so a bad value from the platform never collapses the HUD.
        /// </summary>
        /// <param name="safeArea">Safe area in pixels.</param>
        /// <param name="screenWidth">Screen width in pixels.</param>
        /// <param name="screenHeight">Screen height in pixels.</param>
        /// <param name="anchorMin">Lower-left anchor, from 0 to 1.</param>
        /// <param name="anchorMax">Upper-right anchor, from 0 to 1.</param>
        public static void ComputeAnchors(Rect safeArea, int screenWidth, int screenHeight, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            if (screenWidth <= 0 || screenHeight <= 0 || safeArea.width <= 0f || safeArea.height <= 0f)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            anchorMin = new Vector2(Mathf.Clamp01(safeArea.xMin / screenWidth), Mathf.Clamp01(safeArea.yMin / screenHeight));
            anchorMax = new Vector2(Mathf.Clamp01(safeArea.xMax / screenWidth), Mathf.Clamp01(safeArea.yMax / screenHeight));
        }

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _appliedArea || Screen.width != _appliedSize.x || Screen.height != _appliedSize.y)
            {
                Apply();
            }
        }

        /// <summary>
        /// Sets the anchors from the current safe area and zeroes the offsets, so the rect is exactly the safe area.
        /// </summary>
        private void Apply()
        {
            _appliedArea = Screen.safeArea;
            _appliedSize = new Vector2Int(Screen.width, Screen.height);
            ComputeAnchors(_appliedArea, _appliedSize.x, _appliedSize.y, out var min, out var max);
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
