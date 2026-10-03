using System;
using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// Loads and saves the <see cref="SaveData"/>. It never throws to its callers: a bad file or a failed write
    /// is logged and the game continues with defaults or the last good file (S-100).
    /// </summary>
    public class SaveSystem
    {
        private readonly ISaveStorage _storage;
        private bool _dirty;

        /// <summary>
        /// Creates the system with defaults in <see cref="Data"/> until <see cref="Load"/> runs.
        /// </summary>
        /// <param name="storage">Where the file lives.</param>
        public SaveSystem(ISaveStorage storage)
        {
            _storage = storage;
            Data = SaveData.CreateDefaults();
        }

        /// <summary>The current save. Never null.</summary>
        public SaveData Data { get; private set; }

        /// <summary>Whether a change is waiting for <see cref="FlushIfDirty"/>.</summary>
        public bool IsDirty => _dirty;

        /// <summary>
        /// Reads the file into <see cref="Data"/>. A missing file gives defaults silently. A file that is empty,
        /// corrupt, truncated or of another version is copied to <c>save.bak</c> and replaced by defaults with one warning.
        /// </summary>
        public void Load()
        {
            _dirty = false;
            string json;
            try
            {
                if (!_storage.TryRead(out json))
                {
                    Data = SaveData.CreateDefaults();
                    return;
                }
            }
            catch (Exception e)
            {
                Data = SaveData.CreateDefaults();
                Debug.LogWarning($"Save could not be read, using defaults: {e.Message}");
                return;
            }

            var reason = TryParse(json, out var parsed);
            if (reason == null)
            {
                parsed.Normalize();
                Data = parsed;
                return;
            }

            Data = SaveData.CreateDefaults();
            try
            {
                _storage.Backup();
            }
            catch (Exception e)
            {
                reason += $" (backup failed: {e.Message})";
            }

            Debug.LogWarning($"Save was reset to defaults: {reason}.");
        }

        /// <summary>
        /// Marks the save as changed. The write happens in <see cref="FlushIfDirty"/>, so many requests in one
        /// frame cost one write.
        /// </summary>
        public void RequestSave()
        {
            _dirty = true;
        }

        /// <summary>
        /// Writes the save when a change was requested.
        /// </summary>
        public void FlushIfDirty()
        {
            if (_dirty)
            {
                Save();
            }
        }

        /// <summary>
        /// Writes the save now. A failure is logged and the previous file stays.
        /// </summary>
        /// <returns>True when the file was written.</returns>
        public bool Save()
        {
            _dirty = false;
            try
            {
                _storage.Write(JsonUtility.ToJson(Data));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save could not be written: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses and validates a save.
        /// </summary>
        /// <param name="json">File content.</param>
        /// <param name="data">The parsed save when valid.</param>
        /// <returns>Null when valid, otherwise why it is not.</returns>
        private static string TryParse(string json, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return "the file is empty";
            }

            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                return $"the file is not valid JSON ({e.Message})";
            }

            if (data == null)
            {
                return "the file has no content";
            }

            return data.version == SaveData.CURRENT_VERSION ? null : $"unknown version {data.version}";
        }
    }
}
