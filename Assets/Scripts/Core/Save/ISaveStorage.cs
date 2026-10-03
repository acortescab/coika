namespace Coika.Core
{
    /// <summary>
    /// Raw access to the save file, so the logic can be tested without the disk.
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>
        /// Reads the save file.
        /// </summary>
        /// <param name="json">The file content when it exists.</param>
        /// <returns>True when the file exists.</returns>
        /// <exception cref="System.IO.IOException">The file exists but cannot be read.</exception>
        bool TryRead(out string json);

        /// <summary>
        /// Replaces the save file atomically: the previous file survives a failure.
        /// </summary>
        /// <param name="json">New content.</param>
        /// <exception cref="System.IO.IOException">The file cannot be written.</exception>
        void Write(string json);

        /// <summary>
        /// Keeps a copy of the current file as the backup, replacing an older one.
        /// </summary>
        /// <exception cref="System.IO.IOException">The copy cannot be made.</exception>
        void Backup();
    }
}
