using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;

namespace Coika.Tools
{
    /// <summary>
    /// Checks the permissions of the merged manifest of a built APK: <c>VIBRATE</c> must be there and <c>INTERNET</c>
    /// must not (GDD §17: the game has no network access).
    /// </summary>
    public static class AndroidManifestCheck
    {
        /// <summary>The permission the haptics need.</summary>
        public const string REQUIRED_PERMISSION = "android.permission.VIBRATE";

        /// <summary>The permission that must never be in the store build.</summary>
        public const string FORBIDDEN_PERMISSION = "android.permission.INTERNET";

        private static readonly Regex _permissionPattern = new Regex(@"uses-permission[\w-]*:\s*name='([^']+)'");

        /// <summary>
        /// Extracts the permission names from the output of <c>aapt2 dump permissions</c>.
        /// </summary>
        /// <param name="aapt2Output">The text printed by aapt2.</param>
        /// <returns>The distinct permission names, in order of appearance.</returns>
        public static IReadOnlyList<string> ParsePermissions(string aapt2Output)
        {
            return _permissionPattern.Matches(aapt2Output).Select(match => match.Groups[1].Value).Distinct().ToList();
        }

        /// <summary>
        /// Lists what is wrong with a set of permissions.
        /// </summary>
        /// <param name="permissions">The permissions of the merged manifest.</param>
        /// <returns>One message per problem; empty when the manifest is acceptable.</returns>
        public static IReadOnlyList<string> FindProblems(IEnumerable<string> permissions)
        {
            var problems = new List<string>();
            var granted = permissions.ToList();

            if (granted.Contains(FORBIDDEN_PERMISSION))
            {
                problems.Add($"The manifest declares {FORBIDDEN_PERMISSION}, which the game must not have (GDD §17).");
            }

            if (!granted.Contains(REQUIRED_PERMISSION))
            {
                problems.Add($"The manifest does not declare {REQUIRED_PERMISSION}, which the haptics need.");
            }

            return problems;
        }

        /// <summary>
        /// Dumps the permissions of an APK with the aapt2 of the Android SDK bundled with the Editor and checks them.
        /// </summary>
        /// <param name="apkPath">Path of the built APK.</param>
        /// <exception cref="BuildFailedException">The permissions cannot be read or break the rules.</exception>
        public static void Verify(string apkPath)
        {
            var permissions = ParsePermissions(DumpPermissions(apkPath));
            var problems = FindProblems(permissions);

            if (problems.Count > 0)
            {
                throw new BuildFailedException(string.Join(" ", problems));
            }

            UnityEngine.Debug.Log($"Manifest check passed. Permissions: {string.Join(", ", permissions)}.");
        }

        /// <summary>
        /// Runs <c>aapt2 dump permissions</c> on an APK.
        /// </summary>
        /// <param name="apkPath">Path of the APK.</param>
        /// <returns>The text aapt2 printed.</returns>
        /// <exception cref="BuildFailedException">aapt2 is missing or fails.</exception>
        private static string DumpPermissions(string apkPath)
        {
            var buildTools = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "SDK", "build-tools");
            var aapt2 = Directory.Exists(buildTools)
                ? Directory.GetFiles(buildTools, "aapt2*", SearchOption.AllDirectories)
                    .FirstOrDefault(file => Path.GetFileNameWithoutExtension(file) == "aapt2")
                : null;

            if (aapt2 == null)
            {
                throw new BuildFailedException($"aapt2 was not found under {buildTools}.");
            }

            var info = new ProcessStartInfo(aapt2, $"dump permissions \"{apkPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new BuildFailedException($"aapt2 failed with exit code {process.ExitCode}: {error}");
                }

                return output;
            }
        }
    }
}
