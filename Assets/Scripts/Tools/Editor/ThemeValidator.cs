using System.Collections.Generic;
using Coika.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Validates theme data: the checks of <see cref="ThemeDefinition.Validate"/> plus the C-01 rule that the
    /// theme and tier assets live in the Core-Data group and the tier sprites in the Theme-Cosmic group.
    /// </summary>
    public static class ThemeValidator
    {
        /// <summary>
        /// Validates every ThemeDefinition in the project and logs OK or one error per problem found.
        /// </summary>
        [MenuItem("Coika/Validate Theme")]
        public static void ValidateAllThemes()
        {
            var guids = AssetDatabase.FindAssets("t:ThemeDefinition");
            if (guids.Length == 0)
            {
                Debug.LogError("No ThemeDefinition asset found in the project.");
                return;
            }

            foreach (var guid in guids)
            {
                var theme = AssetDatabase.LoadAssetAtPath<ThemeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var errors = GetErrors(theme);

                if (errors.Count == 0)
                {
                    Debug.Log($"{theme.name}: OK", theme);
                    continue;
                }

                foreach (var error in errors)
                    Debug.LogError($"{theme.name}: {error}", theme);
            }
        }

        /// <summary>
        /// Runs every check on a theme: its own validation and the Addressables group rule.
        /// </summary>
        /// <param name="theme">The theme to validate.</param>
        /// <returns>One message per problem; empty when the theme is valid.</returns>
        public static List<string> GetErrors(ThemeDefinition theme)
        {
            var errors = theme.Validate();
            errors.AddRange(GetAddressableErrors(theme));
            return errors;
        }

        /// <summary>
        /// Lists every asset of the theme that is outside its Addressable group (C-01): the theme and the tiers
        /// belong in Core-Data, the tier sprites in Theme-Cosmic.
        /// </summary>
        /// <param name="theme">The theme to check.</param>
        /// <returns>One message per misplaced asset; empty when every asset is in its group.</returns>
        public static List<string> GetAddressableErrors(ThemeDefinition theme)
        {
            var errors = new List<string>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            CheckEntry(settings, AssetDatabase.GetAssetPath(theme), TierDataSetup.DataGroupName, errors);

            foreach (var tier in PlaceholderTierSpriteGenerator.FindTiers())
            {
                CheckEntry(settings, AssetDatabase.GetAssetPath(tier), TierDataSetup.DataGroupName, errors);

                var spritePath = AssetDatabase.GUIDToAssetPath(tier.Sprite.AssetGUID);
                if (string.IsNullOrEmpty(spritePath))
                    continue; // Missing sprites are reported by ThemeDefinition.Validate

                CheckEntry(settings, spritePath, PlaceholderTierSpriteGenerator.SpriteGroupName, errors);
            }

            return errors;
        }

        /// <summary>
        /// Adds an error when the asset at the given path has no Addressables entry or sits in another group.
        /// </summary>
        /// <param name="settings">Addressables settings to look the asset up in.</param>
        /// <param name="assetPath">Project path of the asset to check.</param>
        /// <param name="expectedGroup">Name of the group the asset must be in.</param>
        /// <param name="errors">List the error is added to.</param>
        private static void CheckEntry(AddressableAssetSettings settings, string assetPath, string expectedGroup, List<string> errors)
        {
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(assetPath));

            if (entry == null)
                errors.Add($"'{assetPath}' is not in an Addressable group (expected '{expectedGroup}').");
            else if (entry.parentGroup.Name != expectedGroup)
                errors.Add($"'{assetPath}' is in group '{entry.parentGroup.Name}', expected '{expectedGroup}'.");
        }
    }
}
