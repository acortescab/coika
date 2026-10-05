using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Test double for <see cref="IAssetService"/>: no bundles needed. Counts loads and releases so tests can
    /// check that every load has a matching release (C-01), and can fail a chosen load attempt.
    /// </summary>
    public class FakeAssetService : IAssetService
    {
        private readonly List<UnityEngine.Object> _created = new();
        private int _attempts;

        /// <summary>1-based load attempt that fails with an AssetLoadException; 0 means never fail.</summary>
        public int FailOnAttempt { get; set; }

        /// <summary>
        /// Creates the asset for a requested type, for types a plain ScriptableObject cannot stand in for (a prefab
        /// GameObject, a Sprite). When null, every load creates a ScriptableObject of the requested type.
        /// </summary>
        public Func<Type, UnityEngine.Object> Provider { get; set; }

        /// <summary>
        /// When set, every load waits for this task before it completes, so a test can leave a load pending, act
        /// meanwhile (dispose the owner, call again) and then let it finish. When null, loads complete at once.
        /// </summary>
        public Task Gate { get; set; }

        /// <summary>Number of loads that succeeded.</summary>
        public int LoadCount { get; private set; }

        /// <summary>Number of assets released.</summary>
        public int ReleaseCount { get; private set; }

        /// <summary>Loads that have not been released yet. Must be 0 at the end of a correct flow.</summary>
        public int OutstandingHandles => LoadCount - ReleaseCount;

        /// <summary>
        /// Returns a new in-memory instance of T for the reference, or a failed task when the attempt is the
        /// one set in <see cref="FailOnAttempt"/>. T must be a ScriptableObject type unless a
        /// <see cref="Provider"/> is set.
        /// </summary>
        /// <typeparam name="T">Type of the asset to create.</typeparam>
        /// <param name="assetReference">Reference whose GUID is used as the key in the failure. May be null.</param>
        public Task<T> LoadAsset<T>(AssetReference assetReference)
        {
            return Load<T>(assetReference?.AssetGUID);
        }

        /// <summary>
        /// Same as <see cref="LoadAsset{T}(AssetReference)"/> for a string key.
        /// </summary>
        /// <typeparam name="T">Type of the asset to create.</typeparam>
        /// <param name="label">Key used in the failure.</param>
        public Task<T> LoadAsset<T>(string label)
        {
            return Load<T>(label);
        }

        /// <summary>
        /// Loads one new instance of T as the content of the label, counted like a single load.
        /// </summary>
        /// <typeparam name="T">Type of the asset to create.</typeparam>
        /// <param name="label">Key used in the failure.</param>
        public async Task<IList<T>> LoadAssets<T>(string label)
        {
            var asset = await Load<T>(label);
            return new List<T> { asset };
        }

        /// <summary>
        /// Counts one release for every asset of the list.
        /// </summary>
        /// <typeparam name="T">Type of the assets.</typeparam>
        /// <param name="assets">The list returned by the load. Null is ignored.</param>
        public void ReleaseAssets<T>(IList<T> assets)
        {
            if (assets != null)
            {
                ReleaseCount += assets.Count;
            }
        }

        /// <summary>
        /// Counts a release. A null object is ignored, like the real service does.
        /// </summary>
        /// <param name="objectToRelease">The asset to release.</param>
        public void ReleaseAsset(UnityEngine.Object objectToRelease)
        {
            if (objectToRelease != null)
                ReleaseCount++;
        }

        /// <summary>Does nothing: there is nothing to preload in a fake.</summary>
        /// <param name="assetReference">Ignored.</param>
        /// <param name="onProgress">Ignored.</param>
        public Task PreloadAsset(AssetReference assetReference, Action<float> onProgress = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>Does nothing: there is nothing to preload in a fake.</summary>
        /// <param name="label">Ignored.</param>
        /// <param name="onProgress">Ignored.</param>
        public Task PreloadAsset(string label, Action<float> onProgress = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>Destroys every object the fake created. Call it from TearDown.</summary>
        public void Cleanup()
        {
            foreach (var created in _created)
                UnityEngine.Object.DestroyImmediate(created);

            _created.Clear();
        }

        /// <summary>
        /// Counts the attempt, then either fails it or creates and tracks a new instance of T.
        /// </summary>
        /// <typeparam name="T">Type of the asset to create.</typeparam>
        /// <param name="key">Key used in the failure.</param>
        private Task<T> Load<T>(string key)
        {
            _attempts++;
            if (_attempts == FailOnAttempt)
                return Task.FromException<T>(new AssetLoadException(key, null));

            return Gate == null ? Task.FromResult(CreateAsset<T>()) : LoadAfterGateAsync<T>();
        }

        /// <summary>
        /// Waits for the gate and then creates the asset, so the load stays pending until the test opens the gate.
        /// </summary>
        /// <typeparam name="T">Type of the asset to create.</typeparam>
        private async Task<T> LoadAfterGateAsync<T>()
        {
            await Gate;
            return CreateAsset<T>();
        }

        /// <summary>
        /// Creates and tracks a new instance of T, and counts the load.
        /// </summary>
        /// <typeparam name="T">Type of the asset to create.</typeparam>
        private T CreateAsset<T>()
        {
            var asset = Provider != null ? Provider(typeof(T)) : ScriptableObject.CreateInstance(typeof(T));
            _created.Add(asset);
            LoadCount++;
            return (T)(object)asset;
        }
    }
}
