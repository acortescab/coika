using System.IO;
using Coika.Core;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// In-memory <see cref="ISaveStorage"/> that counts writes and can simulate a failed replace, in which case
    /// the previous content stays, as the atomic write guarantees.
    /// </summary>
    public class FakeSaveStorage : ISaveStorage
    {
        /// <summary>Content of the save file, or null when there is none.</summary>
        public string Content { get; set; }

        /// <summary>Content of the backup, or null when none was made.</summary>
        public string BackupContent { get; private set; }

        /// <summary>Successful writes so far.</summary>
        public int WriteCount { get; private set; }

        /// <summary>When true, <see cref="Write"/> fails after the temp file, before the replace.</summary>
        public bool FailReplace { get; set; }

        /// <summary>
        /// Reads the content.
        /// </summary>
        /// <param name="json">The content when present.</param>
        /// <returns>True when a file exists.</returns>
        public bool TryRead(out string json)
        {
            json = Content;
            return Content != null;
        }

        /// <summary>
        /// Replaces the content, or fails and keeps the previous one.
        /// </summary>
        /// <param name="json">New content.</param>
        /// <exception cref="IOException">Thrown when <see cref="FailReplace"/> is set.</exception>
        public void Write(string json)
        {
            if (FailReplace)
            {
                throw new IOException("simulated failure between temp write and replace");
            }

            Content = json;
            WriteCount++;
        }

        /// <summary>
        /// Copies the content to the backup.
        /// </summary>
        public void Backup()
        {
            BackupContent = Content;
        }
    }
}
