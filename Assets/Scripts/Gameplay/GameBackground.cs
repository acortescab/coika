using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Placeholder background: one solid-colour sprite stretched to cover much more than the camera reference
    /// frame, so tall or wide phones show the background instead of black bars outside the frame (GDD §8). Final
    /// background art comes from the theme in M3. <see cref="JarCameraFramer"/> calls <see cref="Fit"/> with the
    /// frame it computes, so the background follows the camera framing.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class GameBackground : MonoBehaviour
    {
        /// <summary>How many times larger than the reference frame the background is, in each direction.</summary>
        public const float COVERAGE_FACTOR = 2f;

        private const int SORTING_ORDER = -100; // Behind everything

        /// <summary>
        /// Size the background must have to cover a reference frame with the safety margin.
        /// </summary>
        /// <param name="referenceSize">Size of the reference frame in world units.</param>
        /// <returns>The background size in world units.</returns>
        public static Vector2 ComputeSize(Vector2 referenceSize)
        {
            return referenceSize * COVERAGE_FACTOR;
        }

        /// <summary>
        /// Centres the background on the reference frame and scales its sprite to <see cref="ComputeSize"/>.
        /// Logs an error and does nothing when the sprite renderer has no sprite. Meant to be called once, when the
        /// camera is framed, never per frame.
        /// </summary>
        /// <param name="center">Centre of the reference frame in world units (the camera position).</param>
        /// <param name="referenceSize">Size of the reference frame in world units.</param>
        public void Fit(Vector2 center, Vector2 referenceSize)
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer.sprite == null)
            {
                Debug.LogError("GameBackground needs a sprite.", this);
                return;
            }

            // A solid colour looks the same at any scale, so one stretched quad is enough.
            var spriteSize = spriteRenderer.sprite.bounds.size;
            var size = ComputeSize(referenceSize);

            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            spriteRenderer.sortingOrder = SORTING_ORDER;
            transform.position = new Vector3(center.x, center.y, transform.position.z);
            transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        }
    }
}
