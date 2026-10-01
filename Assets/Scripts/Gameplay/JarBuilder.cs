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

        /// <summary>Thickness of the floor and the walls in world units. Thick colliders prevent tunnelling.</summary>
        public const float WALL_THICKNESS = 1f;

        /// <summary>How far the walls rise above the Drop Line, so pieces can never escape sideways.</summary>
        public const float WALL_CLEARANCE_ABOVE_DROP_LINE = 2f;

        private const string FLOOR_NAME = "Floor";
        private const string LEFT_WALL_NAME = "WallLeft";
        private const string RIGHT_WALL_NAME = "WallRight";

        /// <summary>
        /// Creates or updates the floor and wall colliders of the jar from the config and stores the geometry in
        /// the jar. Safe to call again after the config changes: existing colliders are resized, never duplicated.
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
        }

        /// <summary>
        /// Finds the child with the given name, or creates it, and gives it a box collider with the given shape.
        /// Colliders have no Rigidbody2D, so they are static.
        /// </summary>
        /// <param name="parent">The jar transform that holds the child.</param>
        /// <param name="childName">Name of the child object that holds the collider.</param>
        /// <param name="localCenter">Centre of the collider in the parent's local space.</param>
        /// <param name="size">Size of the collider in world units.</param>
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
        }
    }
}
