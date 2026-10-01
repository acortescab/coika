using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// One piece of the game: a body, a collider and a sprite that <c>PieceFactory</c> configures for a tier. All
    /// pieces (held, falling, merged) are the same prefab. This is the base of the component; its state, the
    /// tier initialization and the collision events are added with the rest of the issue #4 scopes.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D))]
    [DisallowMultipleComponent]
    public class Piece : MonoBehaviour
    {
        /// <summary>Name of the physics layer of a piece that is in play.</summary>
        public const string LAYER_NAME = "Piece";
    }
}
