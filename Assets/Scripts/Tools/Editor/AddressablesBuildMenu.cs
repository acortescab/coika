using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;

namespace Coika.Tools
{
    /// <summary>
    /// Editor menu entry to build the Addressables content without producing a player build.
    /// </summary>
    public static class AddressablesBuildMenu
    {
        /// <summary>
        /// Runs the Addressables content build with the active build script and logs the result.
        /// Needed before testing with the Use Existing Build play mode script.
        /// </summary>
        [MenuItem("Tools/Addressables/Build Content")]
        private static void BuildContent()
        {
            try
            {
                BuildContentOrThrow();
            }
            catch (BuildFailedException exception)
            {
                UnityEngine.Debug.LogError(exception.Message);
            }
        }

        /// <summary>
        /// Runs the Addressables content build with the active build script and logs the duration.
        /// </summary>
        /// <exception cref="BuildFailedException">The content build reports an error.</exception>
        public static void BuildContentOrThrow()
        {
            AddressableAssetSettings.BuildPlayerContent(out var result);

            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new BuildFailedException($"Addressables content build failed: {result.Error}");
            }

            UnityEngine.Debug.Log($"Addressables content build finished in {result.Duration:F1}s.");
        }
    }
}
