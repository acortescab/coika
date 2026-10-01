using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Core
{
    /// <summary>
    /// A reusable pool of instances of an Addressable prefab, so nothing is instantiated or destroyed while the
    /// game runs. It is generic over the component the caller works with, so any system (pieces, effects, UI
    /// items) can pool its prefab the same way.
    /// <para>
    /// C-01: the prefab is Addressable, so it is never loaded lazily during play. <see cref="PrewarmAsync"/> loads
    /// it through <see cref="IAssetService"/> and builds the pool during the load phase, and must be awaited before
    /// the pool is used. The loaded handle is released by <see cref="Dispose"/>, which the owner calls when the
    /// pool is no longer needed. The pool never calls Addressables directly.
    /// </para>
    /// <para>
    /// Items are taken and returned from Update or FixedUpdate, never from inside a collision callback. Every
    /// instance is parented to the container, active or pooled.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Component of the prefab the caller works with. The prefab must have it.</typeparam>
    public sealed class PrefabPool<T> : IDisposable where T : Component
    {
        /// <summary>Number of instances built by <see cref="PrewarmAsync"/> unless another count is given.</summary>
        public const int DEFAULT_PREWARM_COUNT = 40;

        private readonly IAssetService _assets;
        private readonly AssetReference _prefabReference;
        private readonly Transform _container;
        private readonly int _prewarmCount;
        private readonly Action<T> _onRelease;

        private readonly Stack<T> _free = new();
        private readonly List<T> _active = new();
        private readonly ReadOnlyCollection<T> _readOnlyActive;

        private GameObject _prefab;
        private bool _prewarming;
        private bool _warnedAboutGrowth;
        private bool _disposed;

        /// <summary>
        /// Creates a pool. It holds nothing until <see cref="PrewarmAsync"/> runs.
        /// </summary>
        /// <param name="assets">Service used to load and release the prefab.</param>
        /// <param name="prefabReference">Addressable reference to the prefab.</param>
        /// <param name="container">Object that parents every instance, active or pooled.</param>
        /// <param name="prewarmCount">Number of instances built when pre-warming.</param>
        /// <param name="onRelease">Optional cleanup called with an item each time it goes back to the pool.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The pre-warm count is negative.</exception>
        public PrefabPool(IAssetService assets, AssetReference prefabReference, Transform container, int prewarmCount = DEFAULT_PREWARM_COUNT, Action<T> onRelease = null)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _prefabReference = prefabReference ?? throw new ArgumentNullException(nameof(prefabReference));
            _container = container != null ? container : throw new ArgumentNullException(nameof(container));

            if (prewarmCount < 0)
                throw new ArgumentOutOfRangeException(nameof(prewarmCount), "The pre-warm count cannot be negative.");

            _prewarmCount = prewarmCount;
            _onRelease = onRelease;
            _readOnlyActive = new ReadOnlyCollection<T>(_active);
        }

        /// <summary>The items that are in use now. Read-only: items go back to the pool through <see cref="Release"/>.</summary>
        public IReadOnlyList<T> Active => _readOnlyActive;

        /// <summary>Number of items waiting in the pool.</summary>
        public int PooledCount => _free.Count;

        /// <summary>
        /// Loads the prefab and builds the pool of disabled instances. Call it once, during the load phase and
        /// before the run starts.
        /// </summary>
        /// <exception cref="InvalidOperationException">The pool was already pre-warmed or is being pre-warmed, or the prefab has no component of type T.</exception>
        /// <exception cref="ObjectDisposedException">The pool was disposed, possibly while the prefab was loading.</exception>
        /// <exception cref="AssetLoadException">The prefab failed to load.</exception>
        public async Task PrewarmAsync()
        {
            ThrowIfDisposed();

            if (_prefab != null || _prewarming)
                throw new InvalidOperationException("The pool is already pre-warmed or being pre-warmed.");

            _prewarming = true;
            GameObject prefab;
            try
            {
                prefab = await _assets.LoadAsset<GameObject>(_prefabReference);
            }
            finally
            {
                _prewarming = false;
            }

            // The pool may have been disposed while the load was pending. Nothing owns the prefab then, so it is
            // released here instead of leaking.
            if (_disposed)
            {
                _assets.ReleaseAsset(prefab);
                throw new ObjectDisposedException(nameof(PrefabPool<T>));
            }

            if (!prefab.TryGetComponent<T>(out _))
            {
                _assets.ReleaseAsset(prefab);
                throw new InvalidOperationException($"The prefab has no {typeof(T).Name} component.");
            }

            _prefab = prefab;
            for (int i = 0; i < _prewarmCount; i++)
                _free.Push(Spawn());
        }

        /// <summary>
        /// Takes an item from the pool, places it and enables it. If the pool is empty it grows by one instance and
        /// logs a warning the first time, because that instantiates during play. The item keeps whatever state it
        /// had when it was released: the caller initializes it.
        /// </summary>
        /// <param name="position">World position the item is placed at before it is enabled.</param>
        /// <param name="rotation">World rotation the item is placed with.</param>
        /// <returns>The enabled item.</returns>
        /// <exception cref="InvalidOperationException">The pool was not pre-warmed.</exception>
        public T Get(Vector3 position, Quaternion rotation)
        {
            ThrowIfDisposed();

            if (_prefab == null)
                throw new InvalidOperationException("Await PrewarmAsync before getting items.");

            T item;
            if (_free.Count > 0)
            {
                item = _free.Pop();
            }
            else
            {
                WarnAboutGrowth();
                item = Spawn();
            }

            // Placing before enabling lets a body take its pose from the transform when it is enabled.
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            _active.Add(item);
            return item;
        }

        /// <summary>
        /// Takes an item back: it stops being in use, goes through the optional cleanup, is disabled and returns to
        /// the pool. An item that is not in use in this pool is ignored with a warning, and an item that Unity
        /// already destroyed (for example by a scene unload) is forgotten instead of being pooled.
        /// </summary>
        /// <param name="item">The item to take back.</param>
        /// <exception cref="ArgumentNullException">The item is null.</exception>
        public void Release(T item)
        {
            // Not "item == null": that is also true for an object Unity destroyed, which is handled below.
            if (ReferenceEquals(item, null))
                throw new ArgumentNullException(nameof(item));

            if (!_active.Remove(item))
            {
                Debug.LogWarning("Release ignored: the item is not active in this pool.", item);
                return;
            }

            if (item == null)
                return;

            _onRelease?.Invoke(item);
            item.gameObject.SetActive(false);
            _free.Push(item);
        }

        /// <summary>
        /// Destroys every instance, in use or pooled, and releases the prefab. The pool cannot be used afterwards.
        /// Calling it again does nothing.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var item in _active)
                DestroyItem(item);

            foreach (var item in _free)
                DestroyItem(item);

            _active.Clear();
            _free.Clear();

            if (_prefab != null)
            {
                _assets.ReleaseAsset(_prefab);
                _prefab = null;
            }
        }

        /// <summary>
        /// Instantiates one instance of the loaded prefab under the container, disabled.
        /// </summary>
        /// <returns>The component of the new instance.</returns>
        private T Spawn()
        {
            var instance = UnityEngine.Object.Instantiate(_prefab, _container);
            instance.SetActive(false);
            return instance.GetComponent<T>();
        }

        /// <summary>
        /// Logs, only the first time, that the pool was exhausted and had to grow during play.
        /// </summary>
        private void WarnAboutGrowth()
        {
            if (_warnedAboutGrowth)
                return;

            _warnedAboutGrowth = true;
            Debug.LogWarning($"The {typeof(T).Name} pool is exhausted and grew during play. Raise the pre-warm count (now {_prewarmCount}).");
        }

        /// <summary>
        /// Destroys the object of an item, using the call that is valid in the current mode.
        /// </summary>
        /// <param name="item">The item to destroy. An item that is already gone is ignored.</param>
        private static void DestroyItem(T item)
        {
            if (item == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(item.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(item.gameObject);
        }

        /// <summary>
        /// Throws when the pool was disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The pool was disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PrefabPool<T>));
        }
    }
}
