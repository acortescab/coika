using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Rules the test code itself follows (issue #12): tests never use the real clock for gameplay, the global
    /// random generator or the player's data folder, and, like the game (C-01), never load scenes or assets behind the
    /// services' back. <c>ProjectFoundationTests</c> already enforces the C-01 rules on <c>Assets/Scripts</c>; this
    /// fixture applies them to <c>Assets/Tests</c>. Comment lines are ignored, and the two fixtures that hold the
    /// patterns as text are skipped.
    /// </summary>
    public class TestHygieneTests
    {
        private static readonly string[] OwnFixtures = { "TestHygieneTests.cs", "ProjectFoundationTests.cs" };

        /// <summary>
        /// No test reads the real date or time of the machine, uses <c>UnityEngine.Random</c> or reads the persistent
        /// data path, so a test gives the same result on every machine and every day. Timeouts that use
        /// <c>Time.realtimeSinceStartup</c> only bound a wait and are allowed.
        /// </summary>
        [Test]
        public void Tests_Always_DoNotDependOnTheClockTheGlobalRandomOrTheDataFolder()
        {
            var violations = FindViolations(@"\bUnityEngine\.Random\b|\bRandom\.(Range|value|insideUnitCircle|onUnitSphere)\b|\bpersistentDataPath\b|\bDateTime\.(Now|UtcNow|Today)\b|\bEnvironment\.TickCount\b");

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        /// <summary>
        /// No test calls <c>Resources.Load</c>, <c>SceneManager.LoadScene*</c> or an Addressables method directly: they
        /// go through <c>IAssetService</c> and <c>ISceneLoader</c> (C-01).
        /// </summary>
        [Test]
        public void Tests_Always_LoadThroughTheServicesOnly()
        {
            var violations = FindViolations(@"\bResources\.Load|\bSceneManager\.LoadScene|\bAddressables\.[A-Z]\w*\s*[(<]");

            Assert.IsEmpty(violations, string.Join("\n", violations));
        }

        /// <summary>
        /// Looks for a pattern in the code lines of every test source file, not in the comment lines.
        /// </summary>
        /// <param name="pattern">Regular expression to look for.</param>
        /// <returns>One entry per match, as <c>file:line: text</c>.</returns>
        private static List<string> FindViolations(string pattern)
        {
            var regex = new Regex(pattern);
            var violations = new List<string>();
            var root = Path.Combine(Application.dataPath, "Tests");

            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (Array.IndexOf(OwnFixtures, Path.GetFileName(file)) >= 0)
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].TrimStart();
                    if (line.StartsWith("//") || !regex.IsMatch(line))
                    {
                        continue;
                    }

                    violations.Add($"{Path.GetFileName(file)}:{i + 1}: {line}");
                }
            }

            return violations;
        }
    }
}
