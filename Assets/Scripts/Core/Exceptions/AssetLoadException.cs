using System;

namespace Coika.Core
{
    /// <summary>
    /// Exception thrown when an asset or scene fails to load after all retry attempts.
    /// Callers catch it to show a recoverable error state instead of crashing (C-01).
    /// </summary>
    public class AssetLoadException : Exception
    {
        /// <summary>Addressable key, address or label that failed to load.</summary>
        public string Key { get; }

        /// <summary>
        /// Creates the exception for a failed load.
        /// </summary>
        /// <param name="key">Addressable key, address or label that failed to load.</param>
        /// <param name="inner">The error of the last attempt, or null when Addressables gave none.</param>
        public AssetLoadException(string key, Exception inner)
            : base($"Failed to load asset: {key}", inner)
        {
            Key = key;
        }
    }
}
