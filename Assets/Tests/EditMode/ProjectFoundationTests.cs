using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Automated versions of the C-01 acceptance checks (constraints.md) that apply to the project foundation.
    /// </summary>
    public class ProjectFoundationTests
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";

        private static readonly string[] RequiredGroups =
        {
            "Scenes", "Core-Data", "Theme-Cosmic", "Audio-Music", "Audio-SFX", "UI", "FX"
        };

        /// <summary>
        /// Build Settings list only the Boot scene, enabled.
        /// </summary>
        [Test]
        public void BuildSettings_Always_ContainOnlyEnabledBootScene()
        {
            var scenes = EditorBuildSettings.scenes;

            Assert.AreEqual(1, scenes.Length, "Build Settings must list only the Boot scene.");
            Assert.AreEqual(BootScenePath, scenes[0].path);
            Assert.IsTrue(scenes[0].enabled, "The Boot scene must be enabled in Build Settings.");
        }

        /// <summary>
        /// Only the installer and the scene loader may call SceneManager.LoadScene.
        /// </summary>
        [Test]
        public void Scripts_Always_DoNotLoadScenesOutsideBootAndSceneLoader()
        {
            var violations = FindViolations(@"\bSceneManager\.LoadScene", "GameInstaller.cs", "SceneLoaderService.cs");

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        /// <summary>
        /// The project has no Resources folder and no script calls Resources.Load.
        /// </summary>
        [Test]
        public void Project_Always_HasNoResourcesFolderOrResourcesLoad()
        {
            var folders = Directory.GetDirectories(Application.dataPath, "Resources", SearchOption.AllDirectories);
            var violations = FindViolations(@"\bResources\.Load");

            Assert.IsEmpty(folders, "Resources folders are forbidden: " + string.Join(", ", folders));
            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        /// <summary>
        /// Only the services call Addressables directly. GameInstaller is allowed because it calls
        /// Addressables.InitializeAsync (the boot step, not an asset load).
        /// </summary>
        [Test]
        public void Scripts_Always_DoNotCallAddressablesOutsideServices()
        {
            var violations = FindViolations(@"\bAddressables\.", "AssetService.cs", "SceneLoaderService.cs", "GameInstaller.cs");

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        /// <summary>
        /// The Addressables settings exist and contain every required group.
        /// </summary>
        [Test]
        public void AddressablesSettings_Always_HaveRequiredGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            Assert.IsNotNull(settings, "Addressables settings asset not found.");
            foreach (var groupName in RequiredGroups)
                Assert.IsNotNull(settings.FindGroup(groupName), $"Missing Addressables group: {groupName}");
        }

        /// <summary>
        /// The Game scene is Addressable (in the Scenes group) and the Boot scene is not.
        /// </summary>
        [Test]
        public void GameScene_Always_IsAddressableAndBootIsNot()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            var gameEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(GameScenePath));
            var bootEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(BootScenePath));

            Assert.IsNotNull(gameEntry, "The Game scene must be Addressable.");
            Assert.AreEqual("Scenes", gameEntry.parentGroup.Name);
            Assert.IsNull(bootEntry, "The Boot scene must stay in the initial build, not in an Addressables group.");
        }

        /// <summary>
        /// Searches every script under Assets/Scripts for a pattern, ignoring comment lines and the files that
        /// are explicitly allowed to use the API.
        /// </summary>
        /// <param name="pattern">Regular expression to look for.</param>
        /// <param name="allowedFiles">File names that may contain the pattern.</param>
        /// <returns>One "file:line: text" entry per match; empty when there is none.</returns>
        private static List<string> FindViolations(string pattern, params string[] allowedFiles)
        {
            var regex = new Regex(pattern);
            var allowed = new HashSet<string>(allowedFiles);
            var violations = new List<string>();
            var scriptsPath = Path.Combine(Application.dataPath, "Scripts");

            foreach (var file in Directory.GetFiles(scriptsPath, "*.cs", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(file);
                if (allowed.Contains(fileName))
                    continue;

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("//"))
                        continue;

                    if (regex.IsMatch(lines[i]))
                        violations.Add($"{fileName}:{i + 1}: {lines[i].Trim()}");
                }
            }

            return violations;
        }
    }
}
