using System;
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// Typed, observable view of the persisted settings. Setters clamp, ignore unchanged values, raise one
    /// <see cref="Changed"/> event and request one save. Plain C# (S-20): created by the installer, no singleton.
    /// </summary>
    public class SettingsService
    {
        private readonly SaveSystem _save;

        /// <summary>
        /// Creates the service over a save system.
        /// </summary>
        /// <param name="save">Save system that owns the data.</param>
        public SettingsService(SaveSystem save)
        {
            _save = save;
        }

        /// <summary>Raised once per setting that changes value.</summary>
        public event Action<SettingsChanged> Changed;

        /// <summary>Master volume applied over music and effects, 0 to 1.</summary>
        public float Master
        {
            get => Data.master;
            set => SetVolume(SettingKey.Master, ref Data.master, value);
        }

        /// <summary>Music volume, 0 to 1.</summary>
        public float Music
        {
            get => Data.music;
            set => SetVolume(SettingKey.Music, ref Data.music, value);
        }

        /// <summary>Sound effects volume, 0 to 1.</summary>
        public float Sfx
        {
            get => Data.sfx;
            set => SetVolume(SettingKey.Sfx, ref Data.sfx, value);
        }

        /// <summary>Whether haptic feedback is on.</summary>
        public bool Haptics
        {
            get => Data.haptics;
            set => SetFlag(SettingKey.Haptics, ref Data.haptics, value);
        }

        /// <summary>Whether the drop guide line is shown.</summary>
        public bool GuideLine
        {
            get => Data.guideLine;
            set => SetFlag(SettingKey.GuideLine, ref Data.guideLine, value);
        }

        /// <summary>Whether screen shake is reduced.</summary>
        public bool ReduceShake
        {
            get => Data.reduceShake;
            set => SetFlag(SettingKey.ReduceShake, ref Data.reduceShake, value);
        }

        /// <summary>Whether the controls are mirrored for left-handed play.</summary>
        public bool LeftHanded
        {
            get => Data.leftHanded;
            set => SetFlag(SettingKey.LeftHanded, ref Data.leftHanded, value);
        }

        /// <summary>Whether the held piece sits beside the finger so the finger does not cover it.</summary>
        public bool FingerOffset
        {
            get => Data.fingerOffset;
            set => SetFlag(SettingKey.FingerOffset, ref Data.fingerOffset, value);
        }

        /// <summary>Language code. A null or empty value is ignored.</summary>
        public string Language
        {
            get => Data.language;
            set
            {
                if (string.IsNullOrEmpty(value) || value == Data.language)
                {
                    return;
                }

                Data.language = value;
                Raise(new SettingsChanged(SettingKey.Language, 0f, false, value));
            }
        }

        private SettingsData Data => _save.Data.settings;

        /// <summary>
        /// Reads a volume setting by key, so a screen with a row per setting needs no code per setting.
        /// </summary>
        /// <param name="key">Master, Music or Sfx.</param>
        /// <returns>The volume, 0 to 1.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The key is not a volume.</exception>
        public float GetFloat(SettingKey key)
        {
            switch (key)
            {
                case SettingKey.Master:
                    return Master;
                case SettingKey.Music:
                    return Music;
                case SettingKey.Sfx:
                    return Sfx;
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key, "Not a volume setting.");
            }
        }

        /// <summary>
        /// Writes a volume setting by key, with the clamping and the event of its property.
        /// </summary>
        /// <param name="key">Master, Music or Sfx.</param>
        /// <param name="value">The volume, clamped to 0 to 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">The key is not a volume.</exception>
        public void SetFloat(SettingKey key, float value)
        {
            switch (key)
            {
                case SettingKey.Master:
                    Master = value;
                    break;
                case SettingKey.Music:
                    Music = value;
                    break;
                case SettingKey.Sfx:
                    Sfx = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key, "Not a volume setting.");
            }
        }

        /// <summary>
        /// Reads a toggle setting by key.
        /// </summary>
        /// <param name="key">Haptics, GuideLine, ReduceShake, LeftHanded or FingerOffset.</param>
        /// <returns>Whether it is on.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The key is not a toggle.</exception>
        public bool GetBool(SettingKey key)
        {
            switch (key)
            {
                case SettingKey.Haptics:
                    return Haptics;
                case SettingKey.GuideLine:
                    return GuideLine;
                case SettingKey.ReduceShake:
                    return ReduceShake;
                case SettingKey.LeftHanded:
                    return LeftHanded;
                case SettingKey.FingerOffset:
                    return FingerOffset;
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key, "Not a toggle setting.");
            }
        }

        /// <summary>
        /// Writes a toggle setting by key, with the event of its property.
        /// </summary>
        /// <param name="key">Haptics, GuideLine, ReduceShake, LeftHanded or FingerOffset.</param>
        /// <param name="value">Whether it is on.</param>
        /// <exception cref="ArgumentOutOfRangeException">The key is not a toggle.</exception>
        public void SetBool(SettingKey key, bool value)
        {
            switch (key)
            {
                case SettingKey.Haptics:
                    Haptics = value;
                    break;
                case SettingKey.GuideLine:
                    GuideLine = value;
                    break;
                case SettingKey.ReduceShake:
                    ReduceShake = value;
                    break;
                case SettingKey.LeftHanded:
                    LeftHanded = value;
                    break;
                case SettingKey.FingerOffset:
                    FingerOffset = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key, "Not a toggle setting.");
            }
        }

        /// <summary>
        /// Resets scores, totals and discovered tiers and keeps the settings.
        /// </summary>
        public void ResetProgress()
        {
            _save.Data.ResetProgress();
            _save.RequestSave();
        }

        /// <summary>
        /// Sets a volume if it differs after clamping.
        /// </summary>
        /// <param name="key">Setting being changed.</param>
        /// <param name="field">Stored value.</param>
        /// <param name="value">Requested value.</param>
        private void SetVolume(SettingKey key, ref float field, float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(field, value))
            {
                return;
            }

            field = value;
            Raise(new SettingsChanged(key, value, false, null));
        }

        /// <summary>
        /// Sets a toggle if it differs.
        /// </summary>
        /// <param name="key">Setting being changed.</param>
        /// <param name="field">Stored value.</param>
        /// <param name="value">Requested value.</param>
        private void SetFlag(SettingKey key, ref bool field, bool value)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Raise(new SettingsChanged(key, 0f, value, null));
        }

        /// <summary>
        /// Requests a save and notifies the listeners.
        /// </summary>
        /// <param name="change">The change to report.</param>
        private void Raise(SettingsChanged change)
        {
            _save.RequestSave();
            Changed?.Invoke(change);
        }
    }
}
