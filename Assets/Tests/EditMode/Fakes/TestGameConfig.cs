using UnityEditor;
using UnityEngine;
using Coika.Data;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Creates in-memory <see cref="GameConfig"/> instances for tests, so they do not depend on the shipped asset.
    /// </summary>
    public static class TestGameConfig
    {
        /// <summary>
        /// Creates a GameConfig with the given jar values by writing its serialized fields.
        /// </summary>
        /// <param name="jarSize">Interior size of the jar.</param>
        /// <param name="dropLineOffset">Distance from the Danger Line to the Drop Line.</param>
        /// <param name="wallMaterial">Physics material of the walls. May be null.</param>
        /// <returns>The config. The caller must destroy it.</returns>
        public static GameConfig Create(Vector2 jarSize, float dropLineOffset, PhysicsMaterial2D wallMaterial)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var serializedConfig = new SerializedObject(config);
            serializedConfig.FindProperty("_jarSize").vector2Value = jarSize;
            serializedConfig.FindProperty("_dropLineOffset").floatValue = dropLineOffset;
            serializedConfig.FindProperty("_wallMaterial").objectReferenceValue = wallMaterial;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }
    }
}
