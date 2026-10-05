using Coika.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// One-shot, idempotent setup of the visual feedback tuning: creates the FeedbackConfig asset with its default
    /// values, puts it in the Core-Data group (C-01) and wires it into GameConfig.
    /// </summary>
    public static class FeedbackConfigSetup
    {
        /// <summary>Path of the FeedbackConfig asset.</summary>
        public const string FEEDBACK_CONFIG_PATH = "Assets/Data/Feedback/FeedbackConfig.asset";

        private const string GAME_CONFIG_PATH = "Assets/Data/GameConfig/GameConfig.asset";

        /// <summary>
        /// Creates the asset when it is missing, registers it in Core-Data and assigns it to the GameConfig. Aborts
        /// with an error, changing nothing, when the GameConfig or the group is missing.
        /// </summary>
        [MenuItem("Coika/Setup Feedback Config")]
        public static void Run()
        {
            var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>(GAME_CONFIG_PATH);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings != null ? settings.FindGroup(TierDataSetup.DataGroupName) : null;
            if (gameConfig == null || group == null)
            {
                Debug.LogError($"Setup aborted: needs {GAME_CONFIG_PATH} and the '{TierDataSetup.DataGroupName}' group.");
                return;
            }

            var feedback = AssetDatabase.LoadAssetAtPath<FeedbackConfig>(FEEDBACK_CONFIG_PATH);
            if (feedback == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FEEDBACK_CONFIG_PATH));
                feedback = ScriptableObject.CreateInstance<FeedbackConfig>();
                AssetDatabase.CreateAsset(feedback, FEEDBACK_CONFIG_PATH);
            }

            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(FEEDBACK_CONFIG_PATH), group, false, false);

            var serialized = new SerializedObject(gameConfig);
            serialized.FindProperty("_feedback").objectReferenceValue = feedback;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gameConfig);

            AssetDatabase.SaveAssets();
            Debug.Log($"Feedback config is up to date at {FEEDBACK_CONFIG_PATH}.");
        }
    }
}
