using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The rules of the guide line (GDD §6), without any rendering: when it is visible and where a straight drop
    /// of the held piece would first touch something. The landing is a circle cast of the held radius straight
    /// down against the layers given at construction, through the non-allocating overload with a cached filter and
    /// results array (S-50). <see cref="GuideLineView"/> only draws what this class says.
    /// </summary>
    public sealed class GuideLineAim
    {
        // Room for every collider a cast can cross, so the nearest is never cut off by a short array.
        private const int MAX_HITS = 8;

        private readonly ContactFilter2D _filter;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[MAX_HITS];
        private bool _cached;
        private float _lastX;
        private float _lastRadius;
        private float _lastStamp;

        /// <summary>
        /// Creates the aim.
        /// </summary>
        /// <param name="layerMask">Layers the cast can hit: the pieces and the jar walls.</param>
        public GuideLineAim(int layerMask)
        {
            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = layerMask,
                useTriggers = false,
            };
        }

        /// <summary>Whether the last <see cref="Update"/> found something to land on.</summary>
        public bool HasLanding { get; private set; }

        /// <summary>Centre the held piece would have when it first touches something. Valid when <see cref="HasLanding"/>.</summary>
        public Vector2 Landing { get; private set; }

        /// <summary>
        /// Whether the guide is visible: only while the player is pressing to drop (a finger is down, or the drop key
        /// is held) on a held piece, so it never appears right after a drop, during the cooldown, when paused or after
        /// game over, and never when the player turned it off.
        /// </summary>
        /// <param name="pressing">Whether the player is pressing to drop (<see cref="DropController.IsPressing"/>).</param>
        /// <param name="hasHeld">Whether a piece is held.</param>
        /// <param name="settingOn">The Guide Line setting.</param>
        /// <returns>True when the guide should be drawn.</returns>
        public static bool ShouldShow(bool pressing, bool hasHeld, bool settingOn)
        {
            return settingOn && pressing && hasHeld;
        }

        /// <summary>
        /// Forgets the last query, so the next <see cref="Update"/> casts again. Call it when another piece is held.
        /// </summary>
        public void Invalidate()
        {
            _cached = false;
        }

        /// <summary>
        /// Finds the landing point. It casts again only when the X or the radius changed or the physics moved on
        /// (a different <paramref name="stamp"/>), so a piece below that moved is noticed and nothing is cast twice
        /// in a step.
        /// </summary>
        /// <param name="scene">Physics scene to query: the one the held piece lives in.</param>
        /// <param name="origin">Centre of the held piece.</param>
        /// <param name="radius">Radius of the held piece.</param>
        /// <param name="maxDistance">How far down to look: the height of the jar is enough.</param>
        /// <param name="stamp">Changes whenever the physics stepped, for instance <see cref="Time.fixedTime"/>.</param>
        /// <returns>True when the landing was recomputed.</returns>
        public bool Update(PhysicsScene2D scene, Vector2 origin, float radius, float maxDistance, float stamp)
        {
            if (_cached && Mathf.Approximately(origin.x, _lastX) && Mathf.Approximately(radius, _lastRadius) && Mathf.Approximately(stamp, _lastStamp))
            {
                return false;
            }

            _cached = true;
            _lastX = origin.x;
            _lastRadius = radius;
            _lastStamp = stamp;

            var count = scene.CircleCast(origin, radius, Vector2.down, maxDistance, _filter, _hits);
            HasLanding = count > 0;
            Landing = origin;
            var nearest = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                if (_hits[i].distance < nearest)
                {
                    nearest = _hits[i].distance;
                    Landing = _hits[i].centroid;
                }
            }

            return true;
        }
    }
}
