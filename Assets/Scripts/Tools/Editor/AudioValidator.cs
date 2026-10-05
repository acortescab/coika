using System;
using System.Collections.Generic;
using Coika.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace Coika.Tools
{
    /// <summary>
    /// Checks the audio against GDD §11 and constraint C-01: the import settings of every clip, the Addressables
    /// group and label of every clip, that every <see cref="SfxId"/> and <see cref="MusicId"/> has a clip, and that
    /// the mixer is in Core-Data with the exposed MusicVol and SfxVol parameters.
    /// </summary>
    public static class AudioValidator
    {
        /// <summary>
        /// Logs OK or one error per problem.
        /// </summary>
        [MenuItem("Coika/Validate Audio")]
        public static void ValidateAudio()
        {
            var errors = GetErrors();
            if (errors.Count == 0)
            {
                Debug.Log("Audio validation OK.");
                return;
            }

            foreach (var error in errors)
            {
                Debug.LogError(error);
            }
        }

        /// <summary>
        /// Collects every problem of the audio.
        /// </summary>
        /// <returns>One message per problem; empty when all is correct.</returns>
        public static List<string> GetErrors()
        {
            var errors = new List<string>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("Addressables settings not found.");
                return errors;
            }

            CheckClips(settings, AudioSetup.SfxFolder, false, errors);
            CheckClips(settings, AudioSetup.MusicFolder, true, errors);
            CheckNames(errors);
            CheckMixer(settings, errors);
            CheckVoicePrefab(settings, errors);
            return errors;
        }

        /// <summary>
        /// Checks the import settings, group and label of every clip of a folder.
        /// </summary>
        /// <param name="settings">Addressables settings.</param>
        /// <param name="folder">Project folder of the clips.</param>
        /// <param name="isMusic">True for the music folder.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckClips(AddressableAssetSettings settings, string folder, bool isMusic, List<string> errors)
        {
            var expectedGroup = isMusic ? AudioSetup.MusicGroupName : AudioSetup.SfxGroupName;
            foreach (var path in AudioSetup.ClipPaths(folder))
            {
                AudioImportSpec.Check(path, isMusic, errors);

                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                if (entry == null)
                {
                    errors.Add($"{path}: not in an Addressables group.");
                    continue;
                }

                if (entry.parentGroup.Name != expectedGroup)
                {
                    errors.Add($"{path}: in group {entry.parentGroup.Name}, expected {expectedGroup}.");
                }

                var expectedLabel = AudioSetup.LabelFor(path, isMusic);
                if (!entry.labels.Contains(expectedLabel))
                {
                    errors.Add($"{path}: missing label '{expectedLabel}'.");
                }
            }
        }

        /// <summary>
        /// Checks that every sound and track has a clip whose name matches the enum value.
        /// </summary>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckNames(List<string> errors)
        {
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                // Land has numbered variants; every other sound is a single clip named after its id.
                var variants = id == SfxId.Land ? AudioSetup.LAND_VARIANTS : 0;
                for (int variant = variants == 0 ? 0 : 1; variant <= variants; variant++)
                {
                    var suffix = variants == 0 ? string.Empty : variant.ToString();
                    if (AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioSetup.SfxFolder}/{id}{suffix}.wav") == null)
                    {
                        errors.Add($"No clip for SfxId.{id}{suffix}.");
                    }
                }
            }

            foreach (MusicId id in Enum.GetValues(typeof(MusicId)))
            {
                if (AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioSetup.MusicFolder}/{id}.wav") == null)
                {
                    errors.Add($"No clip for MusicId.{id}.");
                }
            }
        }

        /// <summary>
        /// Checks that the pooled voice prefab exists, has an <see cref="AudioVoice"/> and is in the sound effect group.
        /// </summary>
        /// <param name="settings">Addressables settings.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckVoicePrefab(AddressableAssetSettings settings, List<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSetup.VoicePrefabPath);
            if (prefab == null || !prefab.TryGetComponent<AudioVoice>(out var voice) || voice.Source == null)
            {
                errors.Add($"{AudioSetup.VoicePrefabPath}: needs an AudioVoice with its AudioSource.");
                return;
            }

            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AudioSetup.VoicePrefabPath));
            if (entry == null || entry.parentGroup.Name != AudioSetup.SfxGroupName)
            {
                errors.Add($"{AudioSetup.VoicePrefabPath}: must be in group {AudioSetup.SfxGroupName}.");
            }
        }

        /// <summary>
        /// Checks that the mixer exists, is in Core-Data under its address and exposes the two volumes.
        /// </summary>
        /// <param name="settings">Addressables settings.</param>
        /// <param name="errors">Receives the problems.</param>
        private static void CheckMixer(AddressableAssetSettings settings, List<string> errors)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioSetup.MixerPath);
            if (mixer == null)
            {
                errors.Add($"{AudioSetup.MixerPath}: mixer not found.");
                return;
            }

            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AudioSetup.MixerPath));
            if (entry == null || entry.parentGroup.Name != TierDataSetup.DataGroupName)
            {
                errors.Add($"{AudioSetup.MixerPath}: must be in group {TierDataSetup.DataGroupName}.");
            }
            else if (entry.address != AudioManager.MIXER_ADDRESS)
            {
                errors.Add($"{AudioSetup.MixerPath}: address is '{entry.address}', expected '{AudioManager.MIXER_ADDRESS}'.");
            }

            foreach (var parameter in new[] { AudioManager.MUSIC_PARAMETER, AudioManager.SFX_PARAMETER })
            {
                if (!mixer.GetFloat(parameter, out _))
                {
                    errors.Add($"Mixer does not expose '{parameter}'.");
                }
            }

            if (mixer.FindMatchingGroups("Master/Music").Length == 0 || mixer.FindMatchingGroups("Master/SFX").Length == 0)
            {
                errors.Add("Mixer needs the groups Master/Music and Master/SFX.");
            }
        }
    }
}
