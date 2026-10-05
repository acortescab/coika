using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace Coika.Core
{
    /// <summary>
    /// The audio engine (GDD §11, §14.3): a <see cref="PrefabPool{T}"/> of <see cref="VOICE_COUNT"/> voices for sound
    /// effects, one music source and the mixer volumes. The Boot installer creates it on its own object and hands it
    /// out as an <see cref="IAudioService"/>; there is no static instance, so tests use a fake instead.
    /// <para>
    /// It owns no clips. Scenes add and remove their <see cref="SoundBank"/>, so only the sounds of the open scenes
    /// are in memory. The voices are pooled from an Addressable prefab built in <see cref="InitializeAsync"/>, in the
    /// load phase, and a finished voice goes back to the pool in <c>Update</c>; playing a sound allocates nothing and
    /// never instantiates. When all voices are busy the oldest is stolen, so the pool never grows. Volumes follow the
    /// settings live and landing sounds are rate limited by <see cref="LandLimiter"/>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour, IAudioService
    {
        /// <summary>Number of voices in the sound effect pool.</summary>
        public const int VOICE_COUNT = 8;

        /// <summary>Addressable address of the mixer asset (group Core-Data).</summary>
        public const string MIXER_ADDRESS = "audio-mixer";

        /// <summary>Exposed mixer parameter of the music group, in dB.</summary>
        public const string MUSIC_PARAMETER = "MusicVol";

        /// <summary>Exposed mixer parameter of the sound effect group, in dB.</summary>
        public const string SFX_PARAMETER = "SfxVol";

        private readonly List<SoundBank> _banks = new();
        private readonly int[] _variantCursor = new int[Enum.GetValues(typeof(SfxId)).Length];
        private readonly DuckEnvelope _duck = new();
        private PrefabPool<AudioVoice> _voices;
        private AudioMixerGroup _sfxGroup;
        private AudioSource _musicSource;
        private AudioMixer _mixer;
        private SettingsService _settings;
        private Action<SettingsChanged> _onSettingsChanged;
        private Func<double> _clock;
        private LandLimiter _landLimiter;
        private MusicId? _currentMusic;
        private bool _musicPaused;
        private bool _initialized;

        /// <summary>Current music volume sent to the mixer, in dB, including the duck.</summary>
        public float MusicDb { get; private set; }

        /// <summary>Current sound effect volume sent to the mixer, in dB.</summary>
        public float SfxDb { get; private set; }

        /// <summary>Number of voices the pool was built with.</summary>
        public int VoiceCount => _voices != null ? VOICE_COUNT : 0;

        /// <summary>Number of voices that hold a sound right now, playing or about to be reclaimed.</summary>
        public int ActiveVoiceCount => _voices != null ? _voices.Active.Count : 0;

        /// <summary>Number of voices waiting in the pool.</summary>
        public int PooledVoiceCount => _voices != null ? _voices.PooledCount : 0;

        /// <summary>True while a music track is playing and not paused.</summary>
        public bool IsMusicPlaying => _musicSource != null && _musicSource.isPlaying;

        /// <summary>
        /// Builds the voice pool and connects the volumes to the settings. Call it once, in the load phase.
        /// </summary>
        /// <param name="assets">Service that loads the voice prefab.</param>
        /// <param name="voicePrefab">Addressable reference to the voice prefab (an <see cref="AudioVoice"/>).</param>
        /// <param name="mixer">Mixer with the exposed parameters; null to run without one (tests or a failed load).</param>
        /// <param name="settings">Source of the volumes; null for full volume.</param>
        /// <param name="clock">Time in seconds for the rate limit and the voice age; never the real clock in logic (S-63).</param>
        /// <exception cref="ArgumentNullException">The asset service, the prefab or the clock is null.</exception>
        /// <exception cref="InvalidOperationException">The manager was already initialized.</exception>
        /// <exception cref="AssetLoadException">The voice prefab failed to load.</exception>
        public async Task InitializeAsync(IAssetService assets, AssetReference voicePrefab, AudioMixer mixer, SettingsService settings, Func<double> clock)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("AudioManager is already initialized.");
            }

            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            if (voicePrefab == null)
            {
                throw new ArgumentNullException(nameof(voicePrefab));
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _initialized = true;
            _mixer = mixer;
            _settings = settings;
            _landLimiter = new LandLimiter(clock);
            _sfxGroup = FindGroup("Master/SFX");

            _musicSource = CreateMusicSource();

            _voices = new PrefabPool<AudioVoice>(assets, voicePrefab, transform, VOICE_COUNT, voice => voice.Silence());
            await _voices.PrewarmAsync();

            if (_settings != null)
            {
                _onSettingsChanged = HandleSettingsChanged;
                _settings.Changed += _onSettingsChanged;
            }

            ApplyVolumes();
        }

        /// <summary>
        /// Makes the sounds of a bank playable.
        /// </summary>
        /// <param name="bank">A bank whose clips are loaded. Adding it twice does nothing.</param>
        /// <exception cref="ArgumentNullException">The bank is null.</exception>
        public void AddBank(SoundBank bank)
        {
            if (bank == null)
            {
                throw new ArgumentNullException(nameof(bank));
            }

            if (!_banks.Contains(bank))
            {
                _banks.Add(bank);
            }
        }

        /// <summary>
        /// Stops what plays a clip of the bank and forgets the bank.
        /// </summary>
        /// <param name="bank">The bank to remove. A bank that was never added is ignored.</param>
        public void RemoveBank(SoundBank bank)
        {
            if (bank == null || !_banks.Remove(bank))
            {
                return;
            }

            if (_voices != null)
            {
                for (int i = _voices.Active.Count - 1; i >= 0; i--)
                {
                    var voice = _voices.Active[i];
                    if (voice == null || bank.Contains(voice.Source.clip))
                    {
                        _voices.Release(voice);
                    }
                }
            }

            if (_musicSource != null && _musicSource.clip != null && bank.Contains(_musicSource.clip))
            {
                StopMusic();
                _musicSource.clip = null;
            }
        }

        /// <summary>
        /// Plays a sound effect. See <see cref="IAudioService.PlaySfx"/>.
        /// </summary>
        /// <param name="id">The sound to play.</param>
        /// <param name="pitch">Pitch multiplier.</param>
        /// <param name="volume">Volume from 0 to 1.</param>
        public void PlaySfx(SfxId id, float pitch = 1f, float volume = 1f)
        {
            if (_voices == null || !TryFindSfx(id, out var variants))
            {
                return;
            }

            var clip = variants[_variantCursor[(int)id] % variants.Length];
            if (id == SfxId.Land && !_landLimiter.TryAcquire(clip.length / Mathf.Max(pitch, 0.01f)))
            {
                return;
            }

            _variantCursor[(int)id]++;
            var voice = TakeVoice();
            voice.Source.outputAudioMixerGroup = _sfxGroup;
            voice.Play(clip, pitch, volume, _clock());
        }

        /// <summary>
        /// Starts a music loop. See <see cref="IAudioService.PlayMusic"/>.
        /// </summary>
        /// <param name="id">The track to play.</param>
        public void PlayMusic(MusicId id)
        {
            if (_musicSource == null || !TryFindMusic(id, out var clip))
            {
                return;
            }

            _musicPaused = false;
            if (_currentMusic == id && _musicSource.isPlaying)
            {
                return;
            }

            _currentMusic = id;
            _musicSource.clip = clip;
            _musicSource.Play();
        }

        /// <summary>
        /// Stops the music.
        /// </summary>
        public void StopMusic()
        {
            _musicPaused = false;
            _currentMusic = null;
            if (_musicSource != null)
            {
                _musicSource.Stop();
            }
        }

        /// <summary>
        /// Lowers or restores the music volume smoothly.
        /// </summary>
        /// <param name="db">Attenuation in dB; 0 restores the music.</param>
        /// <param name="seconds">Time of the change.</param>
        public void DuckMusic(float db, float seconds)
        {
            _duck.SetTarget(db, seconds);
            ApplyVolumes();
        }

        /// <summary>
        /// Pauses the music where it is.
        /// </summary>
        public void PauseMusic()
        {
            if (_musicSource != null && _musicSource.isPlaying)
            {
                _musicSource.Pause();
                _musicPaused = true;
            }
        }

        /// <summary>
        /// Continues a paused music.
        /// </summary>
        public void ResumeMusic()
        {
            if (_musicSource != null && _musicPaused)
            {
                _musicSource.UnPause();
                _musicPaused = false;
            }
        }

        /// <summary>
        /// Gives finished voices back to the pool and moves the duck toward its target on unscaled time, so it also
        /// runs while the game is paused.
        /// </summary>
        private void Update()
        {
            ReclaimFinishedVoices();

            if (_duck.Advance(Time.unscaledDeltaTime))
            {
                ApplyVolumes();
            }
        }

        /// <summary>
        /// Stops listening to the settings and destroys the voices.
        /// </summary>
        private void OnDestroy()
        {
            if (_settings != null && _onSettingsChanged != null)
            {
                _settings.Changed -= _onSettingsChanged;
            }

            _voices?.Dispose();
            _voices = null;

            // The music source lives on a child that would otherwise outlive a manager destroyed on its own.
            if (_musicSource != null)
            {
                Destroy(_musicSource.gameObject);
            }
        }

        /// <summary>
        /// Silences the game while the app is in the background or interrupted, for example by a call, and brings it
        /// back afterwards. A paused listener also keeps the clips from advancing.
        /// </summary>
        /// <param name="paused">True when the app is pausing.</param>
        private void OnApplicationPause(bool paused)
        {
            AudioListener.pause = paused;
        }

        /// <summary>
        /// Covers the interruptions that only take the focus away, such as a call or the assistant on iOS, where the
        /// app is not paused. Focus coming back lifts the pause unless the app itself is paused.
        /// </summary>
        /// <param name="hasFocus">True when the app has the focus.</param>
        private void OnApplicationFocus(bool hasFocus)
        {
#if UNITY_IOS && !UNITY_EDITOR
            // Only on a device: in the Editor and in unattended test runs the window is often not focused.
            AudioListener.pause = !hasFocus;
#endif
        }

        /// <summary>
        /// Returns to the pool every voice whose sound ended. A paused app keeps its voices.
        /// </summary>
        private void ReclaimFinishedVoices()
        {
            if (_voices == null || AudioListener.pause)
            {
                return;
            }

            for (int i = _voices.Active.Count - 1; i >= 0; i--)
            {
                var voice = _voices.Active[i];
                if (voice == null || !voice.Source.isPlaying)
                {
                    _voices.Release(voice);
                }
            }
        }

        /// <summary>
        /// Takes a voice from the pool, stealing the oldest one when all are busy, so the pool never grows during play.
        /// </summary>
        /// <returns>An enabled voice.</returns>
        private AudioVoice TakeVoice()
        {
            if (_voices.PooledCount == 0)
            {
                AudioVoice oldest = null;
                for (int i = 0; i < _voices.Active.Count; i++)
                {
                    var voice = _voices.Active[i];
                    if (voice != null && (oldest == null || voice.StartedAt < oldest.StartedAt))
                    {
                        oldest = voice;
                    }
                }

                if (oldest != null)
                {
                    _voices.Release(oldest);
                }
            }

            return _voices.Get(Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Finds the clips of a sound in the banks, newest bank first.
        /// </summary>
        /// <param name="id">The sound.</param>
        /// <param name="variants">The clips found.</param>
        /// <returns>True when a bank has the sound.</returns>
        private bool TryFindSfx(SfxId id, out AudioClip[] variants)
        {
            for (int i = _banks.Count - 1; i >= 0; i--)
            {
                if (_banks[i].TryGetSfx(id, out variants))
                {
                    return true;
                }
            }

            variants = null;
            return false;
        }

        /// <summary>
        /// Finds the clip of a track in the banks, newest bank first.
        /// </summary>
        /// <param name="id">The track.</param>
        /// <param name="clip">The clip found.</param>
        /// <returns>True when a bank has the track.</returns>
        private bool TryFindMusic(MusicId id, out AudioClip clip)
        {
            for (int i = _banks.Count - 1; i >= 0; i--)
            {
                if (_banks[i].TryGetMusic(id, out clip))
                {
                    return true;
                }
            }

            clip = null;
            return false;
        }

        /// <summary>
        /// Creates the child object with the looping music source that plays through the Music group.
        /// </summary>
        /// <returns>The source.</returns>
        private AudioSource CreateMusicSource()
        {
            // Inactive while it is set up: a source added to an active object has already started to play on awake.
            var child = new GameObject("Music");
            child.SetActive(false);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.outputAudioMixerGroup = FindGroup("Master/Music");
            child.SetActive(true);
            return source;
        }

        /// <summary>
        /// Finds a mixer group by its path.
        /// </summary>
        /// <param name="path">Group path, for example Master/SFX.</param>
        /// <returns>The group, or null when there is no mixer or no such group.</returns>
        private AudioMixerGroup FindGroup(string path)
        {
            if (_mixer == null)
            {
                return null;
            }

            var groups = _mixer.FindMatchingGroups(path);
            return groups.Length > 0 ? groups[0] : null;
        }

        /// <summary>
        /// Applies a changed volume setting at once.
        /// </summary>
        /// <param name="change">The setting that changed.</param>
        private void HandleSettingsChanged(SettingsChanged change)
        {
            if (change.Key == SettingKey.Master || change.Key == SettingKey.Music || change.Key == SettingKey.Sfx)
            {
                ApplyVolumes();
            }
        }

        /// <summary>
        /// Computes the dB of both groups from the settings (Master multiplies both) and the duck, and sends them to
        /// the mixer.
        /// </summary>
        private void ApplyVolumes()
        {
            var master = _settings != null ? _settings.Master : 1f;
            var music = _settings != null ? _settings.Music : 1f;
            var sfx = _settings != null ? _settings.Sfx : 1f;

            MusicDb = Mathf.Max(AudioMath.VolumeToDb(master * music) + _duck.Current, AudioMath.MIN_DB);
            SfxDb = AudioMath.VolumeToDb(master * sfx);

            if (_mixer != null)
            {
                _mixer.SetFloat(MUSIC_PARAMETER, MusicDb);
                _mixer.SetFloat(SFX_PARAMETER, SfxDb);
            }
        }
    }
}
