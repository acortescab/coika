using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Asset service double for the PlayMode tests (C-01): no Addressables and no built bundles. It hands out an
    /// inactive Piece prefab and a blank sprite, and every task it returns is already completed, so a test can
    /// pre-warm the factory without yielding a frame. The owner destroys what it handed out.
    /// <para>
    /// Given a piece material, the prefab gets the physics settings of the real one (that material, interpolation,
    /// continuous detection), which the simulation harness needs to behave like the game. Without it the prefab is
    /// the plain one the merge tests use.
    /// </para>
    /// </summary>
    public sealed class TestAssetService : IAssetService
    {
        private readonly List<UnityEngine.Object> _created;
        private readonly PhysicsMaterial2D _pieceMaterial;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="created">List that receives every asset the service hands out, so the owner destroys them.</param>
        /// <param name="pieceMaterial">Material of the piece collider, which also gives the prefab the physics settings of the real one. Null for a plain prefab.</param>
        public TestAssetService(List<UnityEngine.Object> created, PhysicsMaterial2D pieceMaterial = null)
        {
            _created = created;
            _pieceMaterial = pieceMaterial;
        }

        /// <summary>
        /// Hands out a test asset of the requested type, whatever the reference.
        /// </summary>
        /// <param name="assetReference">Ignored.</param>
        /// <typeparam name="T">A <see cref="GameObject"/> (the Piece prefab) or a <see cref="Sprite"/>.</typeparam>
        /// <returns>A completed task with the asset.</returns>
        public Task<T> LoadAsset<T>(AssetReference assetReference)
        {
            return Task.FromResult(Provide<T>());
        }

        /// <summary>
        /// Hands out a test asset of the requested type, whatever the label.
        /// </summary>
        /// <param name="label">Ignored.</param>
        /// <typeparam name="T">A <see cref="GameObject"/> (the Piece prefab) or a <see cref="Sprite"/>.</typeparam>
        /// <returns>A completed task with the asset.</returns>
        public Task<T> LoadAsset<T>(string label)
        {
            return Task.FromResult(Provide<T>());
        }

        /// <summary>
        /// Does nothing: the owner destroys the assets it was handed.
        /// </summary>
        /// <param name="objectToRelease">Ignored.</param>
        public void ReleaseAsset(UnityEngine.Object objectToRelease)
        {
        }

        /// <summary>
        /// Does nothing: the test assets are created on demand.
        /// </summary>
        /// <param name="assetReference">Ignored.</param>
        /// <param name="onProgress">Ignored.</param>
        /// <returns>A completed task.</returns>
        public Task PreloadAsset(AssetReference assetReference, Action<float> onProgress = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Does nothing: the test assets are created on demand.
        /// </summary>
        /// <param name="label">Ignored.</param>
        /// <param name="onProgress">Ignored.</param>
        /// <returns>A completed task.</returns>
        public Task PreloadAsset(string label, Action<float> onProgress = null)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Builds a new test asset of the requested type and registers it for destruction.
        /// </summary>
        /// <typeparam name="T">A <see cref="GameObject"/> (the Piece prefab) or a <see cref="Sprite"/>.</typeparam>
        /// <returns>The asset.</returns>
        /// <exception cref="NotSupportedException">Any other type.</exception>
        private T Provide<T>()
        {
            UnityEngine.Object asset;
            if (typeof(T) == typeof(GameObject))
            {
                asset = BuildPiecePrefab();
            }
            else if (typeof(T) == typeof(Sprite))
            {
                var texture = new Texture2D(16, 16);
                _created.Add(texture);
                asset = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), TierDefinition.PixelsPerUnit);
            }
            else
            {
                throw new NotSupportedException(typeof(T).Name);
            }

            _created.Add(asset);
            return (T)(object)asset;
        }

        /// <summary>
        /// Builds the inactive Piece prefab.
        /// </summary>
        /// <returns>The prefab, inactive so the original never takes part in the physics: only its clones do.</returns>
        private GameObject BuildPiecePrefab()
        {
            var prefab = new GameObject("PiecePrefab", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
            prefab.SetActive(false);

            if (_pieceMaterial != null)
            {
                var body = prefab.GetComponent<Rigidbody2D>();
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.sleepMode = RigidbodySleepMode2D.StartAwake;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                prefab.GetComponent<CircleCollider2D>().sharedMaterial = _pieceMaterial;
            }

            return prefab;
        }
    }
}
