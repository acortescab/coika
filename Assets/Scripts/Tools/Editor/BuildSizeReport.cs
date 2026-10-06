using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;

namespace Coika.Tools
{
    /// <summary>
    /// Prints the size of the built packages and of the Addressables groups, and enforces the base size budget of
    /// constraint C-01.
    /// </summary>
    public static class BuildSizeReport
    {
        /// <summary>Base build budget in bytes: 50 MB for M1-M2 (C-01 [TUNE], owner-approved after the first Android build).</summary>
        public const long BUDGET_BYTES = 50L * 1024 * 1024;

        private const double BYTES_PER_MB = 1024.0 * 1024.0;

        /// <summary>
        /// Formats a byte count in megabytes.
        /// </summary>
        /// <param name="bytes">The size in bytes.</param>
        /// <returns>The size with two decimals, for example <c>12.50 MB</c>.</returns>
        public static string FormatMegabytes(long bytes)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F2} MB", bytes / BYTES_PER_MB);
        }

        /// <summary>
        /// Derives the Addressables group name from a bundle file name such as <c>ui_assets_all_0a1b.bundle</c>.
        /// </summary>
        /// <param name="bundleFileName">The file name of a built bundle.</param>
        /// <returns>The part before the first <c>_assets</c> or <c>_scenes</c> marker, or the name without extension.</returns>
        public static string GroupNameOf(string bundleFileName)
        {
            var name = Path.GetFileNameWithoutExtension(bundleFileName);
            var cut = new[] { "_assets", "_scenes" }
                .Select(marker => name.IndexOf(marker, System.StringComparison.Ordinal))
                .Where(index => index > 0)
                .DefaultIfEmpty(-1)
                .Min();
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        /// <summary>
        /// Logs the size of each package and of each Addressables group, then checks every package against the budget.
        /// </summary>
        /// <param name="packagePaths">The built APK and AAB files.</param>
        /// <exception cref="BuildFailedException">A package is bigger than <see cref="BUDGET_BYTES"/>.</exception>
        public static void ReportAndEnforce(IEnumerable<string> packagePaths)
        {
            var report = new StringBuilder("Build size report\n");
            var overBudget = new List<string>();

            foreach (var path in packagePaths)
            {
                var size = new FileInfo(path).Length;
                report.AppendLine($"  {Path.GetFileName(path)}: {FormatMegabytes(size)}");

                if (size > BUDGET_BYTES)
                {
                    overBudget.Add($"{Path.GetFileName(path)} is {FormatMegabytes(size)}");
                }
            }

            report.AppendLine("  Addressables groups:");
            foreach (var group in GroupSizes().OrderByDescending(pair => pair.Value))
            {
                report.AppendLine($"    {group.Key}: {FormatMegabytes(group.Value)}");
            }

            UnityEngine.Debug.Log(report.ToString());

            if (overBudget.Count > 0)
            {
                throw new BuildFailedException($"Over the {FormatMegabytes(BUDGET_BYTES)} base build budget: {string.Join(", ", overBudget)}.");
            }
        }

        /// <summary>
        /// Sums the size of the built Android bundles per Addressables group.
        /// </summary>
        /// <returns>Total bytes per group name.</returns>
        private static Dictionary<string, long> GroupSizes()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var buildPath = settings.profileSettings.EvaluateString(
                settings.activeProfileId,
                settings.profileSettings.GetValueByName(settings.activeProfileId, "Local.BuildPath"));
            var sizes = new Dictionary<string, long>();

            if (!Directory.Exists(buildPath))
            {
                return sizes;
            }

            foreach (var bundle in Directory.GetFiles(buildPath, "*.bundle"))
            {
                var group = GroupNameOf(bundle);
                sizes.TryGetValue(group, out var total);
                sizes[group] = total + new FileInfo(bundle).Length;
            }

            return sizes;
        }
    }
}
