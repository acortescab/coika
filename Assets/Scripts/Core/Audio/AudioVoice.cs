using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// One voice of the sound effect pool: the component of the Addressable voice prefab that the
    /// <see cref="PrefabPool{T}"/> of the <see cref="AudioManager"/> hands out. It holds the source and the time the
    /// current sound started, which tells the manager which voice is the oldest.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioVoice : MonoBehaviour
    {
        [SerializeField]
        private AudioSource _source;

        /// <summary>The source that plays the sound.</summary>
        public AudioSource Source => _source;

        /// <summary>Clock time at which the current sound started, in seconds.</summary>
        public double StartedAt { get; set; }

        /// <summary>
        /// Fills the source if the prefab was built without wiring it.
        /// </summary>
        private void Awake()
        {
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
            }
        }

        /// <summary>
        /// Starts a sound on this voice.
        /// </summary>
        /// <param name="clip">The clip.</param>
        /// <param name="pitch">Pitch multiplier.</param>
        /// <param name="volume">Volume from 0 to 1.</param>
        /// <param name="now">Clock time, kept to know the voice age.</param>
        public void Play(AudioClip clip, float pitch, float volume, double now)
        {
            _source.Stop();
            _source.clip = clip;
            _source.pitch = pitch;
            _source.volume = Mathf.Clamp01(volume);
            _source.Play();
            StartedAt = now;
        }

        /// <summary>
        /// Stops the sound and lets go of the clip, so the bank that owns it can be released.
        /// </summary>
        public void Silence()
        {
            _source.Stop();
            _source.clip = null;
        }
    }
}
