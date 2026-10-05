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
        /// Builds the prefab handed out for a <see cref="GameObject"/> request, for tests that need another prefab
        /// than the Piece one (the audio voice). When null the Piece prefab is used.
        /// </summary>
        public Func<GameObject> PrefabFactory { get; set; }

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
        /// Hands out the audio clips of a label: every sound effect (3 variants of Land) for <c>sfx</c>, the
        /// gameplay track for anything else.
        /// </summary>
        /// <param name="label">The label of the clips.</param>
        /// <typeparam name="T">Must be <see cref="AudioClip"/>.</typeparam>
        /// <returns>A completed task with the clips.</returns>
        /// <exception cref="NotSupportedException">Any other type.</exception>
        public Task<IList<T>> LoadAssets<T>(string label)
        {
            if (typeof(T) != typeof(AudioClip))
            {
                throw new NotSupportedException(typeof(T).Name);
            }

            var names = new List<string>();
            if (label == SoundBank.SFX_LABEL)
            {
                foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                {
                    if (id == SfxId.Land)
                    {
                        names.AddRange(new[] { "Land1", "Land2", "Land3" });
                    }
                    else
                    {
                        names.Add(id.ToString());
                    }
                }
            }
            else
            {
                names.Add(MusicId.Gameplay.ToString());
            }

            IList<T> clips = new List<T>();
            foreach (var clipName in names)
            {
                var clip = AudioClip.Create(clipName, 2205, 1, 22050, false);
                _created.Add(clip);
                clips.Add((T)(object)clip);
            }

            return Task.FromResult(clips);
        }

        /// <summary>
        /// Does nothing: the owner destroys the assets it was handed.
        /// </summary>
        /// <param name="assets">Ignored.</param>
        /// <typeparam name="T">Type of the assets.</typeparam>
        public void ReleaseAssets<T>(IList<T> assets)
        {
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
                asset = PrefabFactory != null ? PrefabFactory() : BuildPiecePrefab();
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
            var prefab = new GameObject("PiecePrefab", typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Piece));
            var visual = new GameObject("Sprite", typeof(SpriteRenderer), typeof(PieceAnimator));
            visual.transform.SetParent(prefab.transform, false);
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
