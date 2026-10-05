using Coika.Core;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// In-memory <see cref="ISaveStorage"/> for the PlayMode tests: nothing touches the disk (TestHygieneTests).
    /// </summary>
    public sealed class TestSaveStorage : ISaveStorage
    {
        private string _content;

        /// <summary>
        /// Reads the content.
        /// </summary>
        /// <param name="json">The content when present.</param>
        /// <returns>True when something was written before.</returns>
        public bool TryRead(out string json)
        {
            json = _content;
            return _content != null;
        }

        /// <summary>
        /// Replaces the content.
        /// </summary>
        /// <param name="json">New content.</param>
        public void Write(string json)
        {
            _content = json;
        }

        /// <summary>
        /// Does nothing: the tests need no backup.
        /// </summary>
        public void Backup()
        {
        }
    }
}
