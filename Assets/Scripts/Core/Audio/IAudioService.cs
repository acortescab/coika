namespace Coika.Core
{
    /// <summary>
    /// What the game needs from the audio engine. Gameplay and UI depend on this interface, so tests can replace the
    /// engine with a fake that records the calls.
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// Plays a sound effect on a free voice, stealing the oldest one when all are busy. A request that cannot
        /// play (clip not loaded, landing rate limit) is dropped silently.
        /// </summary>
        /// <param name="id">The sound to play.</param>
        /// <param name="pitch">Pitch multiplier, 1 for the original.</param>
        /// <param name="volume">Volume from 0 to 1 applied before the mixer.</param>
        void PlaySfx(SfxId id, float pitch = 1f, float volume = 1f);

        /// <summary>
        /// Starts a music loop. Does nothing when that track is already playing.
        /// </summary>
        /// <param name="id">The track to play.</param>
        void PlayMusic(MusicId id);

        /// <summary>
        /// Stops the music.
        /// </summary>
        void StopMusic();

        /// <summary>
        /// Lowers (or restores, with 0) the music volume smoothly.
        /// </summary>
        /// <param name="db">Attenuation in dB; 0 restores the music.</param>
        /// <param name="seconds">Time of the change.</param>
        void DuckMusic(float db, float seconds);

        /// <summary>
        /// Pauses the music where it is, for example while the game is paused.
        /// </summary>
        void PauseMusic();

        /// <summary>
        /// Continues the music paused by <see cref="PauseMusic"/>.
        /// </summary>
        void ResumeMusic();

        /// <summary>
        /// Makes the sounds of a loaded bank playable. The service keeps only a reference: the scene that created the
        /// bank owns its clips and calls <see cref="RemoveBank"/> before it disposes it.
        /// </summary>
        /// <param name="bank">A bank whose clips are loaded.</param>
        void AddBank(SoundBank bank);

        /// <summary>
        /// Stops every sound that plays a clip of the bank and forgets the bank, so it can be disposed.
        /// </summary>
        /// <param name="bank">The bank to remove. A bank that was never added is ignored.</param>
        void RemoveBank(SoundBank bank);
    }
}
