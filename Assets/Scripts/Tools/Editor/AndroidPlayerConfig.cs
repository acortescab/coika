using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// The committed Android player settings of the store build (GDD §14.6), applied from code so that a build can never
    /// drift from them.
    /// </summary>
    public static class AndroidPlayerConfig
    {
        /// <summary>
        /// Sets IL2CPP, ARM64 only, minimum API 26, automatic target API (the highest installed, which satisfies the
        /// current Play requirement), portrait only, Medium managed stripping and Optimized Frame Pacing. The frame rate
        /// cap and the vSync count are set at runtime by the game scene installer.
        /// </summary>
        [MenuItem("Tools/Android/Apply Player Settings")]
        public static void Apply()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            Debug.Log("Android player settings applied.");
        }
    }
}
