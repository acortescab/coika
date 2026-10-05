using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// The audio clips one scene needs, loaded together by Addressable label and released together. The
    /// <see cref="IAudioService"/> keeps no clips of its own: a scene creates a bank in its load phase (C-01), adds it
    /// to the service and removes and disposes it when it unloads, so only the sounds of the scenes that are open sit
    /// in memory however many sounds the game has.
    /// <para>
    /// A clip is matched to a sound by its name: the name of a <see cref="SfxId"/> or of a <see cref="MusicId"/>,
    /// with an optional number at the end for variants (Land1, Land2, Land3).
    /// </para>
    /// </summary>
    public sealed class SoundBank : IDisposable
    {
        /// <summary>Addressable label of the gameplay sound effects.</summary>
        public const string SFX_LABEL = "sfx";

        /// <summary>Addressable label of the gameplay music.</summary>
        public const string MUSIC_GAMEPLAY_LABEL = "music-gameplay";

        private static readonly char[] VariantSuffix = { '_', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        private readonly IAssetService _assets;
        private readonly string[] _labels;
        private readonly Dictionary<SfxId, AudioClip[]> _sfx = new();
        private readonly Dictionary<MusicId, AudioClip> _music = new();
        private readonly List<IList<AudioClip>> _loaded = new();
        private bool _disposed;

        /// <summary>
        /// Creates an empty bank. Nothing loads until <see cref="LoadAsync"/>.
        /// </summary>
        /// <param name="assets">Service that loads and releases the clips.</param>
        /// <param name="labels">Addressable labels whose clips the bank holds.</param>
        /// <exception cref="ArgumentNullException">The service or the labels are null.</exception>
        public SoundBank(IAssetService assets, params string[] labels)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _labels = labels ?? throw new ArgumentNullException(nameof(labels));
        }

        /// <summary>
        /// Loads the clips of every label through the asset service. Call it once, in the load phase.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The bank was disposed, possibly while a load was pending.</exception>
        /// <exception cref="AssetLoadException">A load failed after all retries.</exception>
        public async Task LoadAsync()
        {
            ThrowIfDisposed();

            foreach (var label in _labels)
            {
                var clips = await _assets.LoadAssets<AudioClip>(label);
                if (_disposed)
                {
                    // Disposed while the load was pending: nothing owns these clips, so release them here.
                    _assets.ReleaseAssets(clips);
                    throw new ObjectDisposedException(nameof(SoundBank));
                }

                _loaded.Add(clips);
                Register(clips);
            }
        }

        /// <summary>
        /// Gets the clips of a sound effect.
        /// </summary>
        /// <param name="id">The sound.</param>
        /// <param name="variants">The clips, one per variant, in name order; do not modify the array.</param>
        /// <returns>True when the bank has the sound.</returns>
        public bool TryGetSfx(SfxId id, out AudioClip[] variants)
        {
            return _sfx.TryGetValue(id, out variants) && variants.Length > 0;
        }

        /// <summary>
        /// Gets the clip of a music track.
        /// </summary>
        /// <param name="id">The track.</param>
        /// <param name="clip">The clip.</param>
        /// <returns>True when the bank has the track.</returns>
        public bool TryGetMusic(MusicId id, out AudioClip clip)
        {
            return _music.TryGetValue(id, out clip) && clip != null;
        }

        /// <summary>
        /// Tells whether a clip was loaded by this bank.
        /// </summary>
        /// <param name="clip">The clip.</param>
        /// <returns>True when the bank holds it.</returns>
        public bool Contains(AudioClip clip)
        {
            foreach (var list in _loaded)
            {
                if (list.Contains(clip))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Forgets the clips and releases them. Stop everything that plays them first (see
        /// <see cref="IAudioService.RemoveBank"/>). Calling it again does nothing.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sfx.Clear();
            _music.Clear();
            foreach (var list in _loaded)
            {
                _assets.ReleaseAssets(list);
            }

            _loaded.Clear();
        }

        /// <summary>
        /// Registers loaded clips under the sound named by the clip.
        /// </summary>
        /// <param name="clips">The loaded clips.</param>
        private void Register(IList<AudioClip> clips)
        {
            var variants = new Dictionary<SfxId, List<AudioClip>>();
            foreach (var clip in clips)
            {
                var name = clip.name.TrimEnd(VariantSuffix);
                if (Enum.TryParse(name, true, out SfxId sfxId))
                {
                    if (!variants.TryGetValue(sfxId, out var list))
                    {
                        // Keeps the variants of a sound that an earlier label already provided.
                        list = _sfx.TryGetValue(sfxId, out var existing) ? new List<AudioClip>(existing) : new List<AudioClip>();
                        variants[sfxId] = list;
                    }

                    list.Add(clip);
                }
                else if (Enum.TryParse(name, true, out MusicId musicId))
                {
                    _music[musicId] = clip;
                }
                else
                {
                    Debug.LogWarning($"Audio clip '{clip.name}' matches no SfxId or MusicId.");
                }
            }

            foreach (var pair in variants)
            {
                pair.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                _sfx[pair.Key] = pair.Value.ToArray();
            }
        }

        /// <summary>
        /// Throws when the bank was disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The bank was disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SoundBank));
            }
        }
    }
}
