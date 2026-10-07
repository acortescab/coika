using System.Collections.Generic;
using Coika.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// One-shot, idempotent setup of the tier data: creates Theme_Cosmic with the 11 tiers, puts the data assets in
    /// the Core-Data group and wires GameConfig.theme. It never touches the sprites: those come from
    /// <see cref="PieceArtGenerator"/>.
    /// </summary>
    public static class TierDataSetup
    {
        private const string ThemePath = "Assets/Data/Themes/Theme_Cosmic.asset";
        private const string GameConfigPath = "Assets/Data/GameConfig/GameConfig.asset";
        public const string DataGroupName = "Core-Data";

        /// <summary>
        /// Runs the whole setup and validates the result. Aborts with an error, changing nothing else, when the
        /// 11 tiers, the Core-Data group or the GameConfig asset are missing.
        /// </summary>
        [MenuItem("Coika/Setup Tier Data")]
        public static void Run()
        {
            var tiers = PieceArtRules.FindTiers();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var dataGroup = settings != null ? settings.FindGroup(DataGroupName) : null;
            var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);

            if (tiers.Count != ThemeDefinition.TIER_COUNT || dataGroup == null || gameConfig == null)
            {
                Debug.LogError($"Setup aborted: needs {ThemeDefinition.TIER_COUNT} tiers, the '{DataGroupName}' group and {GameConfigPath}.");
                return;
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeDefinition>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<ThemeDefinition>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            AssignTiers(theme, tiers);

            foreach (var tier in tiers)
                MoveToGroup(settings, dataGroup, AssetDatabase.GetAssetPath(tier));
            MoveToGroup(settings, dataGroup, ThemePath);

            AssignTheme(gameConfig, AssetDatabase.AssetPathToGUID(ThemePath));

            AssetDatabase.SaveAssets();
            ThemeValidator.ValidateAllThemes();
        }

        /// <summary>
        /// Fills the tier list of a theme with references to the given tiers, in the given order.
        /// </summary>
        /// <param name="theme">The theme to modify.</param>
        /// <param name="tiers">The tiers, already sorted by index.</param>
        private static void AssignTiers(ThemeDefinition theme, List<TierDefinition> tiers)
        {
            var serializedTheme = new SerializedObject(theme);
            var list = serializedTheme.FindProperty("_tiers");
            list.arraySize = tiers.Count;

            for (int i = 0; i < tiers.Count; i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tiers[i]));
                list.GetArrayElementAtIndex(i).FindPropertyRelative("m_AssetGUID").stringValue = guid;
            }

            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        /// <summary>
        /// Points GameConfig.theme at the theme with the given GUID.
        /// </summary>
        /// <param name="gameConfig">The config to modify.</param>
        /// <param name="themeGuid">GUID of the theme asset.</param>
        private static void AssignTheme(GameConfig gameConfig, string themeGuid)
        {
            var serializedConfig = new SerializedObject(gameConfig);
            serializedConfig.FindProperty("_theme.m_AssetGUID").stringValue = themeGuid;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gameConfig);
        }

        /// <summary>
        /// Makes an asset Addressable in the given group, creating or moving its entry as needed.
        /// </summary>
        /// <param name="settings">Addressables settings.</param>
        /// <param name="group">Group that must contain the asset.</param>
        /// <param name="assetPath">Project path of the asset.</param>
        private static void MoveToGroup(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath)
        {
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), group, false, false);
        }
    }
}
