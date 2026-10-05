using System.Collections.Generic;
using Coika.Data;
using Coika.Fx;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Builds a real <see cref="ParticleSpawner"/> with the two particle systems of the prefab, but made in code
    /// and with no Addressables, for the tests.
    /// </summary>
    public static class TestParticleSpawner
    {
        /// <summary>
        /// Creates and initializes the spawner. The caller destroys the returned object (or registers it in
        /// <paramref name="created"/>).
        /// </summary>
        /// <param name="created">List that receives the created game object, for later destruction.</param>
        /// <param name="config">The tuning and the caps.</param>
        /// <returns>The initialized spawner.</returns>
        public static ParticleSpawner Create(List<Object> created, FeedbackConfig config)
        {
            var root = new GameObject("TestParticleSpawner");
            created.Add(root);
            var spawner = root.AddComponent<ParticleSpawner>();
            TestReflection.SetField(spawner, "_particles", CreateSystem(root.transform, "Particles"));
            TestReflection.SetField(spawner, "_rings", CreateSystem(root.transform, "Rings"));
            spawner.Initialize(config);
            return spawner;
        }

        /// <summary>
        /// Adds a world-space system that emits nothing by itself, like the prefab's.
        /// </summary>
        private static ParticleSystem CreateSystem(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.enabled = false;
            return system;
        }
    }
}
