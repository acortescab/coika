using System;
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// Persisted user settings with the defaults of GDD §13.
    /// </summary>
    [Serializable]
    public class SettingsData
    {
        /// <summary>Master volume applied over music and effects, 0 to 1.</summary>
        public float master = 1f;

        /// <summary>Music volume, 0 to 1.</summary>
        public float music = 0.8f;

        /// <summary>Sound effects volume, 0 to 1.</summary>
        public float sfx = 1f;

        /// <summary>Whether haptic feedback is on.</summary>
        public bool haptics = true;

        /// <summary>Whether the drop guide line is shown.</summary>
        public bool guideLine = true;

        /// <summary>Whether screen shake is reduced.</summary>
        public bool reduceShake;

        /// <summary>Whether the controls are mirrored for left-handed play.</summary>
        public bool leftHanded;

        /// <summary>Language code.</summary>
        public string language = "en";

        /// <summary>Whether the held piece sits beside the finger instead of keeping its place under it.</summary>
        public bool fingerOffset;

        /// <summary>
        /// Brings the volumes into 0 to 1 and replaces a missing language with the default.
        /// </summary>
        public void Clamp()
        {
            master = Mathf.Clamp01(master);
            music = Mathf.Clamp01(music);
            sfx = Mathf.Clamp01(sfx);
            if (string.IsNullOrEmpty(language))
            {
                language = "en";
            }
        }
    }
}
