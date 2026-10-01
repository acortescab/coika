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
        // Written only by JarBuilder from the GameConfig. Serialized so the prefab keeps its bounds without needing
        // the config, but hidden from the Inspector so nobody edits them by hand and desyncs them from the colliders.
        [SerializeField, HideInInspector]
        private Vector2 _interiorSize;
        [SerializeField, HideInInspector]
        private float _dropLineOffset;
        [SerializeField, HideInInspector]
        private DangerLine _dangerLine;

        /// <summary>The dashed Danger Line of this jar: show it and make it pulse to warn about overflow.</summary>
        public DangerLine DangerLine => _dangerLine;

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

        /// <summary>
        /// Stores the Danger Line the jar was built with. Called by <see cref="JarBuilder"/> after it builds.
        /// </summary>
        /// <param name="dangerLine">The Danger Line child of this jar.</param>
        internal void SetDangerLine(DangerLine dangerLine)
        {
            _dangerLine = dangerLine;
        }

#if UNITY_EDITOR
        // Gizmos are drawn in front of the sprites (which sit at z = 0), so the depth test does not hide the
        // parts that coincide with the walls, the floor or the Danger Line sprite.
        private const float GIZMO_Z = -1f;
        private const float GIZMO_FILL_DEPTH = 0.01f;

        private static readonly Color InteriorGizmoColor = new(0.3f, 0.9f, 0.4f, 1f); // Green
        private static readonly Color InteriorFillGizmoColor = new(0.3f, 0.9f, 0.4f, 0.12f); // Translucent green
        private static readonly Color DangerLineGizmoColor = new(0.95f, 0.25f, 0.25f, 1f); // Red
        private static readonly Color DropLineGizmoColor = new(1f, 0.85f, 0.2f, 1f); // Yellow

        /// <summary>
        /// Draws the interior bounds (outline and a translucent fill), the Danger Line and the Drop Line in the
        /// Scene view, to debug the jar geometry. They can be switched off with the Gizmos menu. Draws nothing
        /// until the jar has been built. Editor only: it is not part of player builds.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (_interiorSize.x <= 0f || _interiorSize.y <= 0f)
                return;

            var min = InteriorMin;
            var max = InteriorMax;
            var bottomLeft = new Vector3(min.x, min.y, GIZMO_Z);
            var bottomRight = new Vector3(max.x, min.y, GIZMO_Z);
            var topRight = new Vector3(max.x, max.y, GIZMO_Z);
            var topLeft = new Vector3(min.x, max.y, GIZMO_Z);

            Gizmos.color = InteriorFillGizmoColor;
            Gizmos.DrawCube((bottomLeft + topRight) * 0.5f, new Vector3(max.x - min.x, max.y - min.y, GIZMO_FILL_DEPTH));

            Gizmos.color = InteriorGizmoColor;
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);

            Gizmos.color = DangerLineGizmoColor;
            Gizmos.DrawLine(new Vector3(min.x, DangerLineY, GIZMO_Z), new Vector3(max.x, DangerLineY, GIZMO_Z));

            Gizmos.color = DropLineGizmoColor;
            Gizmos.DrawLine(new Vector3(min.x, DropLineY, GIZMO_Z), new Vector3(max.x, DropLineY, GIZMO_Z));
        }
#endif
    }
}
