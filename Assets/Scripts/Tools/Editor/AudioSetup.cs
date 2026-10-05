using System;
using System.IO;
using System.Reflection;
using Coika.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

namespace Coika.Tools
{
    /// <summary>
    /// One-shot, idempotent setup of the placeholder audio: writes the sounds and the two music loops, applies the
    /// import settings of GDD §11, creates the mixer (Master > Music, SFX with exposed MusicVol and SfxVol) and
    /// registers everything in the Addressables groups with their labels (C-01). Run it again to regenerate.
    /// </summary>
    public static class AudioSetup
    {
        /// <summary>Folder of the sound effects.</summary>
        public const string SfxFolder = "Assets/Audio/Sfx";

        /// <summary>Folder of the music.</summary>
        public const string MusicFolder = "Assets/Audio/Music";

        /// <summary>Path of the mixer asset.</summary>
        public const string MixerPath = "Assets/Audio/Mixer/GameMixer.mixer";

        /// <summary>Path of the prefab of one pooled voice.</summary>
        public const string VoicePrefabPath = "Assets/Prefabs/Audio/AudioVoice.prefab";

        /// <summary>Number of variants of the landing sound (Land1, Land2, ...).</summary>
        public const int LAND_VARIANTS = 3;

        /// <summary>Addressables group of the sound effects.</summary>
        public const string SfxGroupName = "Audio-SFX";

        /// <summary>Addressables group of the music.</summary>
        public const string MusicGroupName = "Audio-Music";

        /// <summary>Label of the menu music, which M3 will load.</summary>
        public const string MenuLabel = "music-menu";

        private const string BootScenePath = "Assets/Scenes/Boot.unity";

        /// <summary>
        /// Generates the clips, the mixer and the Addressables entries, then validates the result.
        /// </summary>
        [MenuItem("Coika/Setup Audio")]
        public static void Run()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var sfxGroup = settings != null ? settings.FindGroup(SfxGroupName) : null;
            var musicGroup = settings != null ? settings.FindGroup(MusicGroupName) : null;
            var dataGroup = settings != null ? settings.FindGroup(TierDataSetup.DataGroupName) : null;
            if (sfxGroup == null || musicGroup == null || dataGroup == null)
            {
                Debug.LogError($"Setup aborted: needs the '{SfxGroupName}', '{MusicGroupName}' and '{TierDataSetup.DataGroupName}' groups.");
                return;
            }

            WriteClips();
            AssetDatabase.Refresh();

            foreach (var path in ClipPaths(SfxFolder))
            {
                AudioImportSpec.Apply(path, false);
                Register(settings, sfxGroup, path, LabelFor(path, false));
            }

            foreach (var path in ClipPaths(MusicFolder))
            {
                AudioImportSpec.Apply(path, true);
                Register(settings, musicGroup, path, LabelFor(path, true));
            }

            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) == null)
            {
                CreateMixer();
            }

            var mixerEntry = Register(settings, dataGroup, MixerPath, null);
            mixerEntry.address = AudioManager.MIXER_ADDRESS;

            BuildVoicePrefab();
            Register(settings, sfxGroup, VoicePrefabPath, null);
            WireBootScene();

            AssetDatabase.SaveAssets();
            AudioValidator.ValidateAudio();
        }

        /// <summary>
        /// Builds the prefab of one sound effect voice: an audio source that does not play on awake, and the
        /// <see cref="AudioVoice"/> that points at it.
        /// </summary>
        private static void BuildVoicePrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VoicePrefabPath));
            var root = new GameObject("AudioVoice");
            try
            {
                var source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                var voice = root.AddComponent<AudioVoice>();

                var serialized = new SerializedObject(voice);
                serialized.FindProperty("_source").objectReferenceValue = source;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, VoicePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Opens the Boot scene, points the installer at the voice prefab and saves the scene.
        /// </summary>
        private static void WireBootScene()
        {
            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            GameInstaller installer = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                installer = root.GetComponentInChildren<GameInstaller>(true);
                if (installer != null)
                {
                    break;
                }
            }

            if (installer == null)
            {
                Debug.LogError($"Setup Audio: no GameInstaller in {BootScenePath}, the voice prefab was not wired.");
                return;
            }

            var serialized = new SerializedObject(installer);
            serialized.FindProperty("_audioVoice.m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(VoicePrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// The Addressables label a clip must have: sound effects share one, the menu track has its own and any other
        /// music is the gameplay music.
        /// </summary>
        /// <param name="path">Project path of the clip.</param>
        /// <param name="isMusic">True for a music clip.</param>
        /// <returns>The label.</returns>
        public static string LabelFor(string path, bool isMusic)
        {
            if (!isMusic)
            {
                return SoundBank.SFX_LABEL;
            }

            return Path.GetFileNameWithoutExtension(path) == MusicId.Menu.ToString() ? MenuLabel : SoundBank.MUSIC_GAMEPLAY_LABEL;
        }

        /// <summary>
        /// Lists the audio files of a folder.
        /// </summary>
        /// <param name="folder">Project folder.</param>
        /// <returns>Project paths of the WAV files.</returns>
        public static string[] ClipPaths(string folder)
        {
            var paths = Directory.GetFiles(folder, "*.wav");
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = paths[i].Replace('\\', '/');
            }

            return paths;
        }

        /// <summary>
        /// Writes every placeholder sound and loop as a WAV file.
        /// </summary>
        private static void WriteClips()
        {
            const int RATE = AudioImportSpec.SFX_SAMPLE_RATE;
            Directory.CreateDirectory(SfxFolder);
            Directory.CreateDirectory(MusicFolder);

            WriteSfx(SfxId.Spawn.ToString(), ToneSynth.Glide(RATE, 0.12, 600, 820, 4.0));
            WriteSfx(SfxId.Drop.ToString(), ToneSynth.Glide(RATE, 0.16, 320, 140, 5.0));
            for (int variant = 1; variant <= LAND_VARIANTS; variant++)
            {
                WriteSfx($"{SfxId.Land}{variant}", ToneSynth.Thump(RATE, 0.10, 90 + 30 * variant, variant));
            }

            WriteSfx(SfxId.Merge.ToString(), ToneSynth.Glide(RATE, 0.25, 440, 660, 4.0, 0.3));
            WriteSfx(SfxId.MergeBig.ToString(), ToneSynth.Glide(RATE, 0.5, 330, 990, 3.0, 0.5));
            WriteSfx(SfxId.Supernova.ToString(), ToneSynth.Glide(RATE, 1.0, 90, 1400, 2.5, 0.6));
            WriteSfx(SfxId.DangerTick.ToString(), ToneSynth.Glide(RATE, 0.06, 1000, 1000, 8.0));
            WriteSfx(SfxId.GameOver.ToString(), ToneSynth.Glide(RATE, 0.8, 440, 90, 2.0, 0.2));
            WriteSfx(SfxId.NewBest.ToString(), ToneSynth.Concat(
                ToneSynth.Glide(RATE, 0.18, 523, 523, 3.0, 0.3),
                ToneSynth.Glide(RATE, 0.18, 659, 659, 3.0, 0.3),
                ToneSynth.Glide(RATE, 0.18, 784, 784, 3.0, 0.3),
                ToneSynth.Glide(RATE, 0.45, 1047, 1047, 2.5, 0.3)));
            WriteSfx(SfxId.UiClick.ToString(), ToneSynth.Glide(RATE, 0.05, 900, 900, 6.0));
            WriteSfx(SfxId.UiBack.ToString(), ToneSynth.Glide(RATE, 0.07, 600, 400, 5.0));

            const int MUSIC_RATE = AudioImportSpec.MUSIC_SAMPLE_RATE;
            ToneSynth.WriteWav($"{MusicFolder}/{MusicId.Gameplay}.wav", ToneSynth.ArpeggioLoop(MUSIC_RATE, 90, new[] { 220.0, 174.6, 261.6, 196.0 }, 2), MUSIC_RATE, 2);
            ToneSynth.WriteWav($"{MusicFolder}/{MusicId.Menu}.wav", ToneSynth.ArpeggioLoop(MUSIC_RATE, 70, new[] { 261.6, 196.0, 220.0, 174.6 }, 1), MUSIC_RATE, 2);
        }

        /// <summary>
        /// Writes one mono sound effect.
        /// </summary>
        /// <param name="clipName">File name without extension.</param>
        /// <param name="samples">The samples.</param>
        private static void WriteSfx(string clipName, float[] samples)
        {
            ToneSynth.WriteWav($"{SfxFolder}/{clipName}.wav", samples, AudioImportSpec.SFX_SAMPLE_RATE, 1);
        }

        /// <summary>
        /// Makes an asset Addressable in a group with an optional label.
        /// </summary>
        /// <param name="settings">Addressables settings.</param>
        /// <param name="group">Group that must contain the asset.</param>
        /// <param name="path">Project path of the asset.</param>
        /// <param name="label">Label to set, or null for none.</param>
        /// <returns>The entry.</returns>
        private static AddressableAssetEntry Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string label)
        {
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
            if (label != null)
            {
                settings.AddLabel(label);
                entry.SetLabel(label, true, false, false);
            }

            return entry;
        }

        /// <summary>
        /// Creates the mixer with Master > Music and SFX, and exposes the volume of the two children as MusicVol and
        /// SfxVol. The editor API for mixers is internal, so it is reached through reflection.
        /// </summary>
        private static void CreateMixer()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
            var editorAssembly = typeof(Editor).Assembly;
            var controllerType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerController");
            var groupType = editorAssembly.GetType("UnityEditor.Audio.AudioMixerGroupController");
            var exposedType = editorAssembly.GetType("UnityEditor.Audio.ExposedAudioParameter");

            var controller = controllerType.GetMethod("CreateMixerControllerAtPath").Invoke(null, new object[] { MixerPath });
            var master = controllerType.GetProperty("masterGroup").GetValue(controller);
            var createGroup = controllerType.GetMethod("CreateNewGroup");
            var addChild = controllerType.GetMethod("AddChildToParent");

            var music = createGroup.Invoke(controller, new object[] { "Music", true });
            var sfx = createGroup.Invoke(controller, new object[] { "SFX", true });
            addChild.Invoke(controller, new[] { music, master });
            addChild.Invoke(controller, new[] { sfx, master });

            var exposed = Array.CreateInstance(exposedType, 2);
            exposed.SetValue(Exposed(exposedType, groupType, music, AudioManager.MUSIC_PARAMETER), 0);
            exposed.SetValue(Exposed(exposedType, groupType, sfx, AudioManager.SFX_PARAMETER), 1);
            controllerType.GetProperty("exposedParameters").SetValue(controller, exposed);

            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Builds the exposed parameter that points at the volume of a group.
        /// </summary>
        /// <param name="exposedType">The ExposedAudioParameter type.</param>
        /// <param name="groupType">The AudioMixerGroupController type.</param>
        /// <param name="group">The group.</param>
        /// <param name="parameterName">Name of the exposed parameter.</param>
        /// <returns>The boxed struct.</returns>
        private static object Exposed(Type exposedType, Type groupType, object group, string parameterName)
        {
            var parameter = Activator.CreateInstance(exposedType);
            exposedType.GetField("guid").SetValue(parameter, groupType.GetMethod("GetGUIDForVolume").Invoke(group, null));
            exposedType.GetField("name").SetValue(parameter, parameterName);
            return parameter;
        }

    }
}
