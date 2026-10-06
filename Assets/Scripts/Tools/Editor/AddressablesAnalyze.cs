using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;

namespace Coika.Tools
{
    /// <summary>
    /// Runs the Addressables Analyze rules that matter for the store build. Duplicate bundle dependencies must be
    /// zero; the Resources and Boot scene duplicates are logged and accepted (the TextMesh Pro essentials live in
    /// Resources on purpose and Boot is the only player scene).
    /// </summary>
    public static class AddressablesAnalyze
    {
        private const string ISOLATION_GROUP = "Duplicate Asset Isolation";
        private const string SHARED_GROUP = "Core-Data";

        /// <summary>
        /// Checks the rules and exits the Editor with 0 when the bundles have no duplicates, 1 otherwise.
        /// Meant for <c>-executeMethod</c>.
        /// </summary>
        public static void RunInBatchMode()
        {
            try
            {
                RunOrThrow();
                EditorApplication.Exit(0);
            }
            catch (BuildFailedException exception)
            {
                UnityEngine.Debug.LogError(exception.Message);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Applies the fix of the duplicate bundle dependencies rule, then moves the shared assets it isolated from
        /// its "Duplicate Asset Isolation" group into the Core-Data group, which every other bundle already needs, and
        /// removes the isolation group. Saves the settings.
        /// </summary>
        [MenuItem("Tools/Addressables/Fix Duplicate Bundle Dependencies")]
        public static void FixBundleDuplicates()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var rule = new CheckBundleDupeDependencies();
            rule.RefreshAnalysis(settings);
            rule.FixIssues(settings);

            var isolation = settings.FindGroup(ISOLATION_GROUP);
            var shared = settings.FindGroup(SHARED_GROUP);

            if (isolation != null && shared != null)
            {
                foreach (var entry in isolation.entries.ToList())
                {
                    settings.MoveEntry(entry, shared);
                }

                settings.RemoveGroup(isolation);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Runs the three duplicate-dependency rules, logs their issue counts and fails on bundle duplicates.
        /// </summary>
        /// <exception cref="BuildFailedException">The bundles share assets that are not isolated.</exception>
        [MenuItem("Tools/Addressables/Run Analyze Rules")]
        public static void RunOrThrow()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var bundleRule = new CheckBundleDupeDependencies();
            var bundleIssues = Count(bundleRule, settings);
            var resourcesIssues = Count(new CheckResourcesDupeDependencies(), settings);
            var sceneIssues = Count(new CheckSceneDupeDependencies(), settings);
            UnityEngine.Debug.Log($"Analyze: bundle duplicates {bundleIssues}, Resources duplicates {resourcesIssues} (accepted), Boot scene duplicates {sceneIssues} (accepted).");

            if (bundleIssues > 0)
            {
                throw new BuildFailedException($"{bundleIssues} duplicate bundle dependencies. Run Tools > Addressables > Fix Duplicate Bundle Dependencies.");
            }
        }

        /// <summary>
        /// Runs one rule and counts its real issues.
        /// </summary>
        /// <param name="rule">The rule to run.</param>
        /// <param name="settings">The Addressables settings.</param>
        /// <returns>The number of results that are not the "no issues" marker.</returns>
        private static int Count(AnalyzeRule rule, AddressableAssetSettings settings)
        {
            return rule.RefreshAnalysis(settings).Count(result => result.severity != MessageType.None && !result.resultName.Contains("No issues"));
        }
    }
}
