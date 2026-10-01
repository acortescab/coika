using System;
using System.Collections.Generic;
using Coika.Data;
using Coika.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Builds the in-memory pieces of the game for tests that need a real <see cref="PieceFactory"/> over a
    /// <see cref="FakeAssetService"/>: the prefab, the sprites and the tiers. The caller owns the objects created
    /// and destroys them, so they are added to the list it passes in.
    /// </summary>
    public static class PieceFixtures
    {
        /// <summary>
        /// Gives a fake asset service a piece-like object for the prefab and a sprite for the sprites. Use it as the
        /// service's <see cref="FakeAssetService.Provider"/>.
        /// </summary>
        /// <param name="type">Type the factory asked for.</param>
        /// <param name="created">List that receives the objects that must be destroyed at the end of the test.</param>
        /// <returns>The prefab or the sprite.</returns>
        /// <exception cref="NotSupportedException">The type is neither a GameObject nor a Sprite.</exception>
        public static UnityEngine.Object ProvideAsset(Type type, List<UnityEngine.Object> created)
        {
            if (type == typeof(GameObject))
            {
                return new GameObject("PiecePrefab", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
            }

            if (type == typeof(Sprite))
            {
                var texture = new Texture2D(16, 16);
                created.Add(texture);
                return Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), TierDefinition.PixelsPerUnit);
            }

            throw new NotSupportedException(type.Name);
        }

        /// <summary>
        /// Creates an in-memory tier with the given index and diameter and a sprite reference, by writing its
        /// serialized fields.
        /// </summary>
        /// <param name="index">Tier index.</param>
        /// <param name="diameterUnits">Diameter of the tier in world units.</param>
        /// <param name="created">List that receives the tier, to be destroyed at the end of the test.</param>
        /// <returns>The tier.</returns>
        public static TierDefinition CreateTier(int index, float diameterUnits, List<UnityEngine.Object> created)
        {
            var tier = ScriptableObject.CreateInstance<TierDefinition>();
            created.Add(tier);

            var serializedTier = new SerializedObject(tier);
            serializedTier.FindProperty("_index").intValue = index;
            serializedTier.FindProperty("_diameterUnits").floatValue = diameterUnits;
            serializedTier.FindProperty("_sprite.m_AssetGUID").stringValue = "tier-sprite";
            serializedTier.ApplyModifiedPropertiesWithoutUndo();
            return tier;
        }
    }
}
