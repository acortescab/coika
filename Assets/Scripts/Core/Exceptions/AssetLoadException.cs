using System;

namespace Coika.Core
{
    /// <summary>
    /// Exception thrown when an asset or scene fails to load after all retry attempts.
    /// </summary>
    public class AssetLoadException : Exception
    {
        public string Key { get; }

        public AssetLoadException(string key, Exception inner)
            : base($"Failed to load asset: {key}", inner)
        {
            Key = key;
        }
    }
}
