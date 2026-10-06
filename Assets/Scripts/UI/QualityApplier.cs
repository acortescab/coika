using Coika.Core;
using Coika.Fx;
using UnityEngine.Rendering;

namespace Coika.UI
{
    /// <summary>
    /// Applies the device tier to the scene objects Core cannot see (issue #39): the particle count and the
    /// post-processing Volume. Stateless, so the scene installer and the tests share one code path.
    /// </summary>
    public static class QualityApplier
    {
        /// <summary>
        /// Gives the particle spawner and the post-processing Volume the cost of the tier. A missing tier (tests, or a
        /// scene without Boot) means full quality, and a missing object is skipped.
        /// </summary>
        /// <param name="tier">The device tier, or null for full quality.</param>
        /// <param name="particles">The particle spawner, or null when the scene has none.</param>
        /// <param name="postProcessing">The global post-processing Volume, or null when the scene has none.</param>
        public static void Apply(IQualityTier tier, ParticleSpawner particles, Volume postProcessing)
        {
            if (particles != null)
            {
                particles.CountMultiplier = tier?.ParticleCountMultiplier ?? 1f;
            }

            if (postProcessing != null)
            {
                postProcessing.enabled = tier?.PostProcessingEnabled ?? true;
            }
        }
    }
}
