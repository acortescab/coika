using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Coika.Gameplay
{
    /// <summary>
    /// Positions the main camera so the jar is horizontally centred and everything from the floor up to the Drop
    /// Line, plus the HUD margin above it, is inside the Pixel Perfect Camera reference frame (180 x 320 px at
    /// PPU 16 in the GDD). It reads the frame size from the <see cref="PixelPerfectCamera"/> and the jar bounds
    /// from the <see cref="Jar"/>, so the framing follows <c>GameConfig</c> without touching the scene.
    /// <para>
    /// The frame is anchored at the top: its top edge is the Drop Line plus the HUD margin. Whatever height is
    /// left below the floor is the bottom strip, where the evolution chart goes (GDD §8.1). The camera is moved
    /// once, never per frame.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PixelPerfectCamera))]
    [DisallowMultipleComponent]
    public class JarCameraFramer : MonoBehaviour
    {
        // The jar is an object of the same scene, so a direct reference is allowed (C-01).
        [SerializeField]
        private Jar _jar;
        [SerializeField, Min(0f)]
        private float _hudMarginPixels = 40f; // Top ~40 px of the reference frame (issue #3)

        /// <summary>
        /// Frames the jar when the scene starts.
        /// </summary>
        private void Start()
        {
            Frame();
        }

        /// <summary>
        /// Moves the camera so the jar is centred and framed as described in the class summary. Keeps the camera
        /// depth. Logs an error when there is no jar, and a warning when the jar does not fit in the reference
        /// frame or the Pixel Perfect Camera would add black bars. Also available from the component menu.
        /// </summary>
        [ContextMenu("Frame Jar")]
        public void Frame()
        {
            if (_jar == null)
            {
                Debug.LogError("JarCameraFramer needs a Jar to frame.", this);
                return;
            }

            var pixelPerfect = GetComponent<PixelPerfectCamera>();
            var pixelsPerUnit = pixelPerfect.assetsPPU;
            var referenceSize = new Vector2(pixelPerfect.refResolutionX, pixelPerfect.refResolutionY) / pixelsPerUnit;
            var hudMargin = _hudMarginPixels / pixelsPerUnit;

            if (pixelPerfect.cropFrame != PixelPerfectCamera.CropFrame.None)
                Debug.LogWarning("The Pixel Perfect Camera crop frame should be None, otherwise tall or wide phones show black bars instead of the background.", this);

            var interiorWidth = _jar.InteriorMax.x - _jar.InteriorMin.x;
            var floorToDropLine = _jar.DropLineY - _jar.FloorY;
            if (!Fits(interiorWidth, floorToDropLine, referenceSize, hudMargin, JarBuilder.WALL_THICKNESS))
                Debug.LogWarning("The jar does not fit in the Pixel Perfect Camera reference frame.", this);

            var center = ComputeCenter(_jar.InteriorMin, _jar.InteriorMax, _jar.DropLineY, referenceSize.y, hudMargin, pixelsPerUnit);
            var position = transform.position;
            transform.position = new Vector3(center.x, center.y, position.z);
        }

        /// <summary>
        /// Where the camera must be so the jar is centred horizontally and the frame's top edge sits at the Drop
        /// Line plus the HUD margin. The result is snapped to the pixel grid, because the Pixel Perfect Camera
        /// needs the camera on whole pixels to keep the pixel look.
        /// </summary>
        /// <param name="interiorMin">Bottom-left corner of the jar interior in world units.</param>
        /// <param name="interiorMax">Top-right corner of the jar interior in world units.</param>
        /// <param name="dropLineY">World Y of the Drop Line.</param>
        /// <param name="referenceHeight">Height of the reference frame in world units.</param>
        /// <param name="hudMargin">Space kept above the Drop Line for the HUD, in world units.</param>
        /// <param name="pixelsPerUnit">Pixels per world unit, used to snap to whole pixels.</param>
        /// <returns>The camera centre in world units.</returns>
        public static Vector2 ComputeCenter(Vector2 interiorMin, Vector2 interiorMax, float dropLineY, float referenceHeight, float hudMargin, float pixelsPerUnit)
        {
            var x = (interiorMin.x + interiorMax.x) * 0.5f;
            var y = dropLineY + hudMargin - referenceHeight * 0.5f;
            return new Vector2(SnapToPixel(x, pixelsPerUnit), SnapToPixel(y, pixelsPerUnit));
        }

        /// <summary>
        /// Whether the jar fits in the reference frame: the interior is not wider than the frame, and the span from
        /// the underside of the floor to the Drop Line plus the HUD margin is not taller than the frame.
        /// </summary>
        /// <param name="interiorWidth">Interior width of the jar in world units.</param>
        /// <param name="floorToDropLine">Distance from the floor surface to the Drop Line in world units.</param>
        /// <param name="referenceSize">Size of the reference frame in world units.</param>
        /// <param name="hudMargin">Space kept above the Drop Line for the HUD, in world units.</param>
        /// <param name="floorThickness">Thickness of the floor in world units.</param>
        /// <returns>True when everything fits.</returns>
        public static bool Fits(float interiorWidth, float floorToDropLine, Vector2 referenceSize, float hudMargin, float floorThickness)
        {
            return interiorWidth <= referenceSize.x && floorToDropLine + hudMargin + floorThickness <= referenceSize.y;
        }

        /// <summary>
        /// Rounds a world coordinate to the nearest whole pixel.
        /// </summary>
        /// <param name="value">Coordinate in world units.</param>
        /// <param name="pixelsPerUnit">Pixels per world unit.</param>
        private static float SnapToPixel(float value, float pixelsPerUnit)
        {
            return Mathf.Round(value * pixelsPerUnit) / pixelsPerUnit;
        }
    }
}
