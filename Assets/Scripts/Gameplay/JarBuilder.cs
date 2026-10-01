using System;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Builds the physical jar of a <see cref="Jar"/> from a <see cref="GameConfig"/>: a floor and two side walls
    /// made of box colliders, with an open top. It is stateless and lives outside the prefab: whoever owns the
    /// config (the Editor tool that bakes the prefab, or the installer at runtime) calls <see cref="Build"/>.
    /// Nothing runs per frame, and the jar never references the config (C-01: it is in another Addressable group).
    /// </summary>
    public static class JarBuilder
    {
        /// <summary>Name of the physics layer of the jar colliders.</summary>
        public const string WALL_LAYER_NAME = "Wall";

        /// <summary>
        /// Thickness of the floor and the walls in world units, for the colliders and for the visuals. Thick
        /// colliders prevent tunnelling. With the interior width, the two walls make the jar 12 units (192 px) wide,
        /// which is the width of the camera reference frame, so the walls are fully visible (GDD §8).
        /// </summary>
        public const float WALL_THICKNESS = 1f;

        /// <summary>How far the walls rise above the Drop Line, so pieces can never escape sideways.</summary>
        public const float WALL_CLEARANCE_ABOVE_DROP_LINE = 2f;

        /// <summary>Sorting order of the floor and walls. Below the Danger Line, which draws at 10.</summary>
        public const int WALL_SORTING_ORDER = 1;

        /// <summary>Name of the floor child. The Editor tool uses it to pick the sprite.</summary>
        public const string FLOOR_NAME = "Floor";

        /// <summary>Name of the left wall child. The Editor tool uses it to pick the sprite.</summary>
        public const string LEFT_WALL_NAME = "WallLeft";

        /// <summary>Name of the right wall child. The Editor tool uses it to pick the sprite.</summary>
        public const string RIGHT_WALL_NAME = "WallRight";

        private const string DANGER_LINE_NAME = "DangerLine";

        /// <summary>
        /// Creates or updates the floor and wall colliders and the Danger Line of the jar from the config, and
        /// stores the geometry in the jar. Safe to call again after the config changes: existing objects are
        /// resized, never duplicated.
        /// Logs an error and builds nothing when the jar size is not positive or the Wall layer does not exist.
        /// </summary>
        /// <param name="jar">The jar to build. Its transform is the centre of the interior floor surface.</param>
        /// <param name="config">Source of the jar size, the Drop Line offset and the wall material.</param>
        /// <exception cref="ArgumentNullException">The jar or the config is null.</exception>
        public static void Build(Jar jar, GameConfig config)
        {
            if (jar == null)
                throw new ArgumentNullException(nameof(jar));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var size = config.JarSize;
            if (size.x <= 0f || size.y <= 0f)
            {
                Debug.LogError($"Jar size must be positive, got {size}.", jar);
                return;
            }

            var wallLayer = LayerMask.NameToLayer(WALL_LAYER_NAME);
            if (wallLayer < 0)
            {
                Debug.LogError($"Physics layer '{WALL_LAYER_NAME}' is not defined.", jar);
                return;
            }

            jar.SetGeometry(size, config.DropLineOffset);

            // Walls run from the underside of the floor up to the clearance above the Drop Line.
            var wallTop = size.y + config.DropLineOffset + WALL_CLEARANCE_ABOVE_DROP_LINE;
            var wallHeight = wallTop + WALL_THICKNESS;
            var wallCenterY = (wallTop - WALL_THICKNESS) * 0.5f;
            var wallCenterX = size.x * 0.5f + WALL_THICKNESS * 0.5f;
            var material = config.WallMaterial;

            // The floor is wider than the interior so it joins the walls with no gap at the corners.
            ConfigureBox(jar.transform, FLOOR_NAME, new Vector2(0f, -WALL_THICKNESS * 0.5f), new Vector2(size.x + 2f * WALL_THICKNESS, WALL_THICKNESS), wallLayer, material);
            ConfigureBox(jar.transform, LEFT_WALL_NAME, new Vector2(-wallCenterX, wallCenterY), new Vector2(WALL_THICKNESS, wallHeight), wallLayer, material);
            ConfigureBox(jar.transform, RIGHT_WALL_NAME, new Vector2(wallCenterX, wallCenterY), new Vector2(WALL_THICKNESS, wallHeight), wallLayer, material);

            ConfigureDangerLine(jar, size);
        }

        /// <summary>
        /// Finds the Danger Line child of the jar, or creates it hidden, and places it at the Danger Line height
        /// with the interior width. An existing line keeps its visibility and pulse state.
        /// </summary>
        /// <param name="jar">The jar that owns the line.</param>
        /// <param name="size">Interior size of the jar in world units.</param>
        private static void ConfigureDangerLine(Jar jar, Vector2 size)
        {
            var child = jar.transform.Find(DANGER_LINE_NAME);
            var isNew = child == null;
            if (isNew)
            {
                child = new GameObject(DANGER_LINE_NAME).transform;
                child.SetParent(jar.transform, false);
            }

            child.SetLocalPositionAndRotation(new Vector3(0f, size.y, 0f), Quaternion.identity);
            child.localScale = Vector3.one;

            if (!child.TryGetComponent<DangerLine>(out var line))
                line = child.gameObject.AddComponent<DangerLine>();

            line.Configure(size.x);
            if (isNew)
                line.SetVisible(false);

            jar.SetDangerLine(line);
        }

        /// <summary>
        /// Finds the child with the given name, or creates it, and gives it a box collider with the given shape and
        /// a sprite renderer that draws the same rectangle. Colliders have no Rigidbody2D, so they are static.
        /// </summary>
        /// <param name="parent">The jar transform that holds the child.</param>
        /// <param name="childName">Name of the child object that holds the collider and the visual.</param>
        /// <param name="localCenter">Centre of the collider and the visual in the parent's local space.</param>
        /// <param name="size">Size of the collider and of the visual in world units.</param>
        /// <param name="layer">Physics layer of the child.</param>
        /// <param name="material">Physics material of the collider. May be null.</param>
        private static void ConfigureBox(Transform parent, string childName, Vector2 localCenter, Vector2 size, int layer, PhysicsMaterial2D material)
        {
            var child = parent.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(parent, false);
            }

            child.gameObject.layer = layer;
            child.SetLocalPositionAndRotation(localCenter, Quaternion.identity);
            child.localScale = Vector3.one;

            if (!child.TryGetComponent<BoxCollider2D>(out var box))
                box = child.gameObject.AddComponent<BoxCollider2D>();

            box.offset = Vector2.zero;
            box.size = size;
            box.sharedMaterial = material;

            ConfigureVisual(child, size);
        }

        /// <summary>
        /// Gives a floor or wall child a sprite renderer that tiles its sprite over the same rectangle as the
        /// collider, so visuals and colliders resize together. The sprite itself is art and is assigned by the
        /// Editor tool; this method never loads assets. Unity resets the size of a tiled sprite renderer whenever
        /// its sprite changes, so build again after assigning sprites.
        /// </summary>
        /// <param name="child">The floor or wall object.</param>
        /// <param name="size">Size of the rectangle in world units.</param>
        private static void ConfigureVisual(Transform child, Vector2 size)
        {
            if (!child.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                spriteRenderer = child.gameObject.AddComponent<SpriteRenderer>();

            spriteRenderer.drawMode = SpriteDrawMode.Tiled;
            spriteRenderer.tileMode = SpriteTileMode.Continuous;
            spriteRenderer.size = size;
            spriteRenderer.sortingOrder = WALL_SORTING_ORDER;
        }
    }
}
