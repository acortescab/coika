using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;

namespace Coika.Tools
{
    /// <summary>
    /// One-shot, idempotent setup of Unity Localization for the UI: the English locale, the localization settings
    /// asset, and the "UI" string table with the keys of <see cref="UiStrings"/>. Unity Localization puts its
    /// tables and locales in Addressables groups by itself (C-01).
    /// </summary>
    public static class LocalizationSetup
    {
        private const string RootFolder = "Assets/Localization";
        private const string LocalesFolder = "Assets/Localization/Locales";
        private const string TablesFolder = "Assets/Localization/Tables";
        private const string SettingsPath = "Assets/Localization/Localization Settings.asset";
        private const string EnglishCode = "en";

        /// <summary>
        /// Creates what is missing and fills the table. Safe to run again: existing locales, settings and
        /// entries are kept, and the English texts are refreshed from <see cref="UiStrings.All"/>.
        /// </summary>
        [MenuItem("Coika/Setup Localization")]
        public static void Run()
        {
            EnsureFolders();
            EnsureEnglishLocale();
            EnsureSettings();
            FillTable(EnsureTable());
            AssetDatabase.SaveAssets();
            Debug.Log("Localization is up to date: locale 'en' and string table 'UI'.");
        }

        /// <summary>
        /// Creates the folders of the localization assets when they do not exist.
        /// </summary>
        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Localization");
            CreateFolder(RootFolder, "Locales");
            CreateFolder(RootFolder, "Tables");
        }

        /// <summary>
        /// Creates a folder under a parent when it is missing.
        /// </summary>
        private static void CreateFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        /// <summary>
        /// Creates the English locale and registers it in the project when it is missing.
        /// </summary>
        private static void EnsureEnglishLocale()
        {
            var id = new LocaleIdentifier(EnglishCode);
            if (LocalizationEditorSettings.GetLocale(id) != null)
            {
                return;
            }

            var locale = Locale.CreateLocale(id);
            AssetDatabase.CreateAsset(locale, $"{LocalesFolder}/English ({EnglishCode}).asset");
            LocalizationEditorSettings.AddLocale(locale);
        }

        /// <summary>
        /// Creates the localization settings and makes them the active ones when the project has none. The new
        /// settings start in English, so the game does not depend on the language of the device.
        /// </summary>
        private static void EnsureSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null)
            {
                return;
            }

            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier(EnglishCode) });
            AssetDatabase.CreateAsset(settings, SettingsPath);
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        /// <summary>
        /// Returns the "UI" string table collection, creating it when it is missing.
        /// </summary>
        private static StringTableCollection EnsureTable()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(UiStrings.TableName);
            return collection != null ? collection : LocalizationEditorSettings.CreateStringTableCollection(UiStrings.TableName, TablesFolder);
        }

        /// <summary>
        /// Adds every key of <see cref="UiStrings.All"/> with its English text, or refreshes the text when the
        /// key exists.
        /// </summary>
        private static void FillTable(StringTableCollection collection)
        {
            var table = collection.GetTable(new LocaleIdentifier(EnglishCode)) as StringTable;
            if (table == null)
            {
                Debug.LogError("The 'UI' string table has no English table.");
                return;
            }

            foreach (var (key, english) in UiStrings.All)
            {
                var entry = table.GetEntry(key) ?? table.AddEntry(key, english);
                entry.Value = english;
            }

            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
        }
    }
}
