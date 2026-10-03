using System;
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
            return Build(serializedConfig =>
            {
                serializedConfig.FindProperty("_jarSize").vector2Value = jarSize;
                serializedConfig.FindProperty("_dropLineOffset").floatValue = dropLineOffset;
                serializedConfig.FindProperty("_wallMaterial").objectReferenceValue = wallMaterial;
            });
        }

        /// <summary>
        /// Creates a GameConfig with the given combo and Supernova values by writing its serialized fields. The other
        /// values keep their defaults.
        /// </summary>
        /// <param name="comboWindow">Seconds within which a merge continues the combo.</param>
        /// <param name="comboStep">Amount the multiplier grows with each merge of the combo.</param>
        /// <param name="comboCap">Highest multiplier.</param>
        /// <param name="superNovaBonus">Points of a Supernova.</param>
        /// <returns>The config. The caller must destroy it.</returns>
        public static GameConfig CreateWithScoring(float comboWindow, float comboStep, int comboCap, int superNovaBonus)
        {
            return Build(serializedConfig =>
            {
                serializedConfig.FindProperty("_comboWindow").floatValue = comboWindow;
                serializedConfig.FindProperty("_comboStep").floatValue = comboStep;
                serializedConfig.FindProperty("_comboCap").intValue = comboCap;
                serializedConfig.FindProperty("_superNovaBonus").intValue = superNovaBonus;
            });
        }

        /// <summary>
        /// Creates a GameConfig with the given spawn values by writing its serialized fields. Applying them runs the
        /// config's OnValidate, which logs an error for every invalid value, so a test that passes invalid values
        /// must expect those logs.
        /// </summary>
        /// <param name="weights">Relative weight of each spawnable tier.</param>
        /// <param name="spawnableTierCount">Number of spawnable tiers.</param>
        /// <param name="antiStreakMax">How many times in a row the same tier may appear.</param>
        /// <param name="forcedOpening">Tiers of the first pieces of a run.</param>
        /// <returns>The config. The caller must destroy it.</returns>
        public static GameConfig CreateWithSpawn(float[] weights, int spawnableTierCount, int antiStreakMax, int[] forcedOpening)
        {
            return Build(serializedConfig =>
            {
                var weightsProperty = serializedConfig.FindProperty("_spawnWeights");
                weightsProperty.arraySize = weights.Length;
                for (int i = 0; i < weights.Length; i++)
                {
                    weightsProperty.GetArrayElementAtIndex(i).floatValue = weights[i];
                }

                var openingProperty = serializedConfig.FindProperty("_forcedOpeningTiers");
                openingProperty.arraySize = forcedOpening.Length;
                for (int i = 0; i < forcedOpening.Length; i++)
                {
                    openingProperty.GetArrayElementAtIndex(i).intValue = forcedOpening[i];
                }

                serializedConfig.FindProperty("_spawnableTierCount").intValue = spawnableTierCount;
                serializedConfig.FindProperty("_antiStreakMax").intValue = antiStreakMax;
            });
        }

        /// <summary>
        /// Creates a GameConfig, lets the caller write its serialized fields, and applies them.
        /// </summary>
        /// <param name="fill">Writes the fields to set.</param>
        /// <returns>The config. The caller must destroy it.</returns>
        private static GameConfig Build(Action<SerializedObject> fill)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var serializedConfig = new SerializedObject(config);
            fill(serializedConfig);
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }
    }
}
