using Coika.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks that the drop tunables of <see cref="GameConfig"/> (issue #6) hold the GDD values, in a new config
    /// and in the asset that ships with the project.
    /// </summary>
    public class GameConfigDropTests
    {
        private const string GAME_CONFIG_PATH = "Assets/Data/GameConfig/GameConfig.asset";

        /// <summary>
        /// A new config has the GDD values: 40 units per second, a 0.5 s cooldown and a 0.15 s scale-in.
        /// </summary>
        [Test]
        public void DropValues_InANewConfig_AreTheGddValues()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                AssertGddValues(config);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        /// <summary>
        /// The shipped asset has the same values, so the tunables are not left at zero by a missing serialized field.
        /// </summary>
        [Test]
        public void DropValues_InTheShippedGameConfig_AreTheGddValues()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>(GAME_CONFIG_PATH);
            Assert.IsNotNull(shipped, $"{GAME_CONFIG_PATH} not found.");

            AssertGddValues(shipped);
        }

        /// <summary>
        /// Checks the follow speed, the cooldown and the scale-in time.
        /// </summary>
        /// <param name="config">The config to check.</param>
        private static void AssertGddValues(GameConfig config)
        {
            Assert.AreEqual(40f, config.MaxFollowSpeed);
            Assert.AreEqual(0.5f, config.DropCooldown);
            Assert.AreEqual(0.15f, config.ScaleInDuration);
        }
    }
}
