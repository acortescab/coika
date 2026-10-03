using System;
using System.Collections.Generic;
using Coika.Data;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Builds in-memory tier lists for the PlayMode tests, so nothing is loaded through Addressables.
    /// </summary>
    public static class TestTiers
    {
        /// <summary>Number of tiers, like the real theme (tier 10 is the Black Hole).</summary>
        public const int TIER_COUNT = 11;

        /// <summary>
        /// Builds the tiers in tier order and registers them for destruction by the caller.
        /// </summary>
        /// <param name="created">List that receives every tier, so the owner destroys them.</param>
        /// <param name="diameter">Diameter in world units of a tier, by index.</param>
        /// <param name="mergeScore">Merge score of a tier, by index.</param>
        /// <returns>The tiers.</returns>
        public static IReadOnlyList<TierDefinition> Build(List<UnityEngine.Object> created, Func<int, float> diameter, Func<int, float> mergeScore)
        {
            var tiers = new List<TierDefinition>();
            for (var i = 0; i < TIER_COUNT; i++)
            {
                var tier = ScriptableObject.CreateInstance<TierDefinition>();
                created.Add(tier);
                TestReflection.SetField(tier, "_index", i);
                TestReflection.SetField(tier, "_diameterUnits", diameter(i));
                TestReflection.SetField(tier, "_radius", diameter(i) * 0.5f);
                TestReflection.SetField(tier, "_mergeScore", mergeScore(i));
                tiers.Add(tier);
            }

            return tiers;
        }
    }
}
