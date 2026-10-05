using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Import settings the audio must have (GDD §11): sound effects are mono 22.05 kHz Vorbis decompressed on load,
    /// music is stereo 44.1 kHz Vorbis streamed. The setup applies them and the validator checks them.
    /// </summary>
    public static class AudioImportSpec
    {
        /// <summary>Sample rate of the sound effects.</summary>
        public const int SFX_SAMPLE_RATE = 22050;

        /// <summary>Sample rate of the music.</summary>
        public const int MUSIC_SAMPLE_RATE = 44100;

        /// <summary>
        /// Applies the settings to an audio file and reimports it.
        /// </summary>
        /// <param name="path">Project path of the audio file.</param>
        /// <param name="isMusic">True for music, false for a sound effect.</param>
        public static void Apply(string path, bool isMusic)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = !isMusic;
            importer.loadInBackground = false;

            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = !isMusic;
            settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.5f;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = (uint)(isMusic ? MUSIC_SAMPLE_RATE : SFX_SAMPLE_RATE);
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Lists the ways an audio file differs from the settings.
        /// </summary>
        /// <param name="path">Project path of the audio file.</param>
        /// <param name="isMusic">True for music, false for a sound effect.</param>
        /// <param name="errors">Receives one message per problem.</param>
        public static void Check(string path, bool isMusic, List<string> errors)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
            {
                errors.Add($"{path}: not an audio file.");
                return;
            }

            var settings = importer.defaultSampleSettings;
            var expectedLoad = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            var expectedRate = isMusic ? MUSIC_SAMPLE_RATE : SFX_SAMPLE_RATE;

            if (settings.loadType != expectedLoad)
            {
                errors.Add($"{path}: load type is {settings.loadType}, expected {expectedLoad}.");
            }

            if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                errors.Add($"{path}: compression is {settings.compressionFormat}, expected Vorbis.");
            }

            if (settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate || settings.sampleRateOverride != expectedRate)
            {
                errors.Add($"{path}: sample rate is not overridden to {expectedRate} Hz.");
            }

            if (importer.forceToMono == isMusic)
            {
                errors.Add($"{path}: Force To Mono is {importer.forceToMono}, expected {!isMusic}.");
            }
        }
    }
}
