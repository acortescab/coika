using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Coika.Tools
{
    /// <summary>
    /// Batch entry point of the Android build: <c>Tools/build-android.ps1</c> calls <see cref="Build"/> through
    /// <c>-executeMethod</c>. It builds the Addressables content, then a testing APK and a store AAB, and exits with a
    /// non-zero code on any failure.
    /// </summary>
    public static class AndroidBuild
    {
        private const string OUTPUT_ARGUMENT = "-buildOutput";
        private const string DEFAULT_OUTPUT_DIRECTORY = "Builds";
        private const string PACKAGE_NAME = "coika";

        /// <summary>
        /// Builds content, APK and AAB into the output directory, checks the manifest and the size budget and exits
        /// the Editor with code 0 on success or 1 on failure.
        /// </summary>
        public static void Build()
        {
            try
            {
                var outputDirectory = ReadOutputDirectory();
                var packages = BuildPackages(outputDirectory);
                BuildSizeReport.ReportAndEnforce(packages);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"Android build failed: {exception.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Applies the settings, builds the content and both packages, and verifies the manifest of the APK.
        /// </summary>
        /// <param name="outputDirectory">Directory that receives the APK and the AAB.</param>
        /// <returns>The paths of the APK and the AAB.</returns>
        /// <exception cref="BuildFailedException">Any step fails.</exception>
        private static string[] BuildPackages(string outputDirectory)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                throw new BuildFailedException("The active build target is not Android. Pass -buildTarget Android.");
            }

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No scene is enabled in the Build Settings.");
            }

            Directory.CreateDirectory(outputDirectory);
            AndroidPlayerConfig.Apply();
            AddressablesBuildMenu.BuildContentOrThrow();

            var wasBundle = EditorUserBuildSettings.buildAppBundle;
            var release = false;
            try
            {
                release = AndroidSigning.Apply();
                UnityEngine.Debug.Log(release ? "Signing with the release keystore." : "Signing with the debug keystore.");

                var apk = BuildPackage(scenes, Path.Combine(outputDirectory, PACKAGE_NAME + ".apk"), false);
                AndroidManifestCheck.Verify(apk);
                var aab = BuildPackage(scenes, Path.Combine(outputDirectory, PACKAGE_NAME + ".aab"), true);
                return new[] { apk, aab };
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = wasBundle;

                if (release)
                {
                    AndroidSigning.Clear();
                }
            }
        }

        /// <summary>
        /// Builds one package.
        /// </summary>
        /// <param name="scenes">The scenes of the Build Settings.</param>
        /// <param name="path">Output file path.</param>
        /// <param name="appBundle">True for an AAB, false for an APK.</param>
        /// <returns>The path of the built file.</returns>
        /// <exception cref="BuildFailedException">The player build does not succeed.</exception>
        private static string BuildPackage(string[] scenes, string path, bool appBundle)
        {
            EditorUserBuildSettings.buildAppBundle = appBundle;
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            var summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Building {Path.GetFileName(path)} ended with {summary.result} ({summary.totalErrors} errors).");
            }

            return path;
        }

        /// <summary>
        /// Reads the <c>-buildOutput</c> command line argument that <c>unity build --output-path</c> forwards.
        /// </summary>
        /// <returns>The output directory, <c>Builds</c> when the argument is absent.</returns>
        private static string ReadOutputDirectory()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, OUTPUT_ARGUMENT);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : DEFAULT_OUTPUT_DIRECTORY;
        }
    }
}
