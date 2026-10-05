using System.Collections.Generic;
using Coika.Core;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// <see cref="IAudioService"/> double that records every call, so tests of the feedback (#34) and the UI (#40) can
    /// assert on what was played without any sound.
    /// </summary>
    public sealed class FakeAudioService : IAudioService
    {
        /// <summary>One recorded sound effect request.</summary>
        public readonly struct SfxCall
        {
            /// <summary>The sound.</summary>
            public readonly SfxId Id;

            /// <summary>The pitch.</summary>
            public readonly float Pitch;

            /// <summary>The volume.</summary>
            public readonly float Volume;

            /// <summary>
            /// Creates the record.
            /// </summary>
            /// <param name="id">The sound.</param>
            /// <param name="pitch">The pitch.</param>
            /// <param name="volume">The volume.</param>
            public SfxCall(SfxId id, float pitch, float volume)
            {
                Id = id;
                Pitch = pitch;
                Volume = volume;
            }
        }

        /// <summary>Every <see cref="PlaySfx"/> request, in order.</summary>
        public List<SfxCall> SfxCalls { get; } = new();

        /// <summary>Every <see cref="PlayMusic"/> request, in order.</summary>
        public List<MusicId> MusicCalls { get; } = new();

        /// <summary>Every <see cref="DuckMusic"/> request as (dB, seconds), in order.</summary>
        public List<(float Db, float Seconds)> DuckCalls { get; } = new();

        /// <summary>The banks added and not yet removed.</summary>
        public List<SoundBank> Banks { get; } = new();

        /// <summary>Number of <see cref="StopMusic"/> calls.</summary>
        public int StopMusicCount { get; private set; }

        /// <summary>Number of <see cref="PauseMusic"/> calls.</summary>
        public int PauseMusicCount { get; private set; }

        /// <summary>Number of <see cref="ResumeMusic"/> calls.</summary>
        public int ResumeMusicCount { get; private set; }

        /// <summary>
        /// Records a sound effect request.
        /// </summary>
        /// <param name="id">The sound.</param>
        /// <param name="pitch">The pitch.</param>
        /// <param name="volume">The volume.</param>
        public void PlaySfx(SfxId id, float pitch = 1f, float volume = 1f)
        {
            SfxCalls.Add(new SfxCall(id, pitch, volume));
        }

        /// <summary>
        /// Records a music request.
        /// </summary>
        /// <param name="id">The track.</param>
        public void PlayMusic(MusicId id)
        {
            MusicCalls.Add(id);
        }

        /// <summary>
        /// Counts a stop.
        /// </summary>
        public void StopMusic()
        {
            StopMusicCount++;
        }

        /// <summary>
        /// Records a duck.
        /// </summary>
        /// <param name="db">Attenuation in dB.</param>
        /// <param name="seconds">Time of the change.</param>
        public void DuckMusic(float db, float seconds)
        {
            DuckCalls.Add((db, seconds));
        }

        /// <summary>
        /// Counts a pause.
        /// </summary>
        public void PauseMusic()
        {
            PauseMusicCount++;
        }

        /// <summary>
        /// Counts a resume.
        /// </summary>
        public void ResumeMusic()
        {
            ResumeMusicCount++;
        }

        /// <summary>
        /// Records a bank.
        /// </summary>
        /// <param name="bank">The bank.</param>
        public void AddBank(SoundBank bank)
        {
            Banks.Add(bank);
        }

        /// <summary>
        /// Forgets a bank.
        /// </summary>
        /// <param name="bank">The bank.</param>
        public void RemoveBank(SoundBank bank)
        {
            Banks.Remove(bank);
        }
    }
}
