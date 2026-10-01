using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The jar in the scene: it only holds the geometry of the last build and exposes the bounds and the key
    /// heights (Danger Line, Drop Line) that the rest of the gameplay reads. It has no construction logic;
    /// <see cref="JarBuilder"/> creates the colliders and sets the geometry.
    /// <para>
    /// The transform position of this object is the centre of the interior floor surface: the interior spans
    /// horizontally around it and upwards from it. All values are in world units.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class Jar : MonoBehaviour
    {
        // Geometry of the last build. Serialized so the prefab keeps its bounds without needing the config.
        [SerializeField]
        private Vector2 _interiorSize;
        [SerializeField]
        private float _dropLineOffset;

        /// <summary>Bottom-left corner of the interior, in world units.</summary>
        public Vector2 InteriorMin => new(transform.position.x - _interiorSize.x * 0.5f, transform.position.y);

        /// <summary>Top-right corner of the interior (at the Danger Line height), in world units.</summary>
        public Vector2 InteriorMax => new(transform.position.x + _interiorSize.x * 0.5f, transform.position.y + _interiorSize.y);

        /// <summary>World Y of the interior floor surface.</summary>
        public float FloorY => transform.position.y;

        /// <summary>World Y of the Danger Line: the floor plus the interior height.</summary>
        public float DangerLineY => FloorY + _interiorSize.y;

        /// <summary>World Y of the Drop Line, where the held piece waits: the Danger Line plus the configured offset.</summary>
        public float DropLineY => DangerLineY + _dropLineOffset;

        /// <summary>
        /// Stores the geometry the jar was built with. Called by <see cref="JarBuilder"/> after it builds.
        /// </summary>
        /// <param name="interiorSize">Interior width and height in world units.</param>
        /// <param name="dropLineOffset">Distance from the Danger Line up to the Drop Line.</param>
        internal void SetGeometry(Vector2 interiorSize, float dropLineOffset)
        {
            _interiorSize = interiorSize;
            _dropLineOffset = dropLineOffset;
        }
    }
}
