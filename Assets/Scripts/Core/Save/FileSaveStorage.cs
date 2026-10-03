using System.IO;

namespace Coika.Core
{
    /// <summary>
    /// Stores the save as <c>save.json</c> in an injected folder. Writes go to <c>save.tmp</c> first and then
    /// replace the file, so a crash in between keeps the previous save.
    /// </summary>
    public class FileSaveStorage : ISaveStorage
    {
        private const string SAVE_FILE = "save.json";
        private const string TEMP_FILE = "save.tmp";
        private const string BACKUP_FILE = "save.bak";

        private readonly string _directory;

        /// <summary>
        /// Creates the storage.
        /// </summary>
        /// <param name="directory">Folder that holds the files; the app passes the persistent data path.</param>
        public FileSaveStorage(string directory)
        {
            _directory = directory;
        }

        /// <summary>
        /// Reads the save file.
        /// </summary>
        /// <param name="json">The file content when it exists.</param>
        /// <returns>True when the file exists.</returns>
        /// <exception cref="IOException">The file exists but cannot be read.</exception>
        public bool TryRead(out string json)
        {
            var path = Path.Combine(_directory, SAVE_FILE);
            if (!File.Exists(path))
            {
                json = null;
                return false;
            }

            json = File.ReadAllText(path);
            return true;
        }

        /// <summary>
        /// Writes <c>save.tmp</c> and replaces <c>save.json</c> with it.
        /// </summary>
        /// <param name="json">New content.</param>
        /// <exception cref="IOException">The file cannot be written.</exception>
        public void Write(string json)
        {
            Directory.CreateDirectory(_directory);
            var path = Path.Combine(_directory, SAVE_FILE);
            var temp = Path.Combine(_directory, TEMP_FILE);
            File.WriteAllText(temp, json);

            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        /// <summary>
        /// Copies <c>save.json</c> to <c>save.bak</c>.
        /// </summary>
        /// <exception cref="IOException">The copy cannot be made.</exception>
        public void Backup()
        {
            var path = Path.Combine(_directory, SAVE_FILE);
            if (File.Exists(path))
            {
                File.Copy(path, Path.Combine(_directory, BACKUP_FILE), true);
            }
        }
    }
}
