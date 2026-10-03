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
