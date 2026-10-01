using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace Coika.Tools
{
    /// <summary>
    /// Editor menu entry to build the Addressables content without producing a player build.
    /// </summary>
    public static class AddressablesBuildMenu
    {
        [MenuItem("Tools/Addressables/Build Content")]
        private static void BuildContent()
        {
            AddressableAssetSettings.BuildPlayerContent(out var result);

            if (!string.IsNullOrEmpty(result.Error))
                UnityEngine.Debug.LogError($"Addressables content build failed: {result.Error}");
            else
                UnityEngine.Debug.Log($"Addressables content build finished in {result.Duration:F1}s.");
        }
    }
}
