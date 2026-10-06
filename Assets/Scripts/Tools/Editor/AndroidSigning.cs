using System;
using UnityEditor;
using UnityEditor.Build;

namespace Coika.Tools
{
    /// <summary>
    /// Chooses the keystore of an Android build: the debug keystore by default, the release keystore when the
    /// <c>COIKA_KEYSTORE_PATH</c> environment variable is set. Secrets are only read from the environment and never
    /// stay in the project settings.
    /// </summary>
    public static class AndroidSigning
    {
        /// <summary>Environment variable with the path of the release keystore.</summary>
        public const string KEYSTORE_PATH_VAR = "COIKA_KEYSTORE_PATH";

        /// <summary>Environment variable with the password of the release keystore.</summary>
        public const string KEYSTORE_PASS_VAR = "COIKA_KEYSTORE_PASS";

        /// <summary>Environment variable with the alias of the release key.</summary>
        public const string KEY_ALIAS_VAR = "COIKA_KEY_ALIAS";

        /// <summary>Environment variable with the password of the release key.</summary>
        public const string KEY_PASS_VAR = "COIKA_KEY_PASS";

        private static string _originalKeystoreName = string.Empty;

        /// <summary>
        /// Selects the keystore for the build that follows.
        /// </summary>
        /// <returns>True when the release keystore is used, false for the debug keystore.</returns>
        /// <exception cref="BuildFailedException">The release keystore path is set but another variable is missing.</exception>
        public static bool Apply()
        {
            var path = Environment.GetEnvironmentVariable(KEYSTORE_PATH_VAR);

            if (string.IsNullOrEmpty(path))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                return false;
            }

            _originalKeystoreName = PlayerSettings.Android.keystoreName;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = path;
            PlayerSettings.Android.keystorePass = Require(KEYSTORE_PASS_VAR);
            PlayerSettings.Android.keyaliasName = Require(KEY_ALIAS_VAR);
            PlayerSettings.Android.keyaliasPass = Require(KEY_PASS_VAR);
            return true;
        }

        /// <summary>
        /// Clears the keystore data from the player settings so that no secret is saved with the project.
        /// </summary>
        public static void Clear()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = _originalKeystoreName;
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasName = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;
        }

        /// <summary>
        /// Reads a mandatory environment variable.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <returns>Its value.</returns>
        /// <exception cref="BuildFailedException">The variable is not set.</exception>
        private static string Require(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);

            if (string.IsNullOrEmpty(value))
            {
                throw new BuildFailedException($"{name} must be set when {KEYSTORE_PATH_VAR} is set.");
            }

            return value;
        }
    }
}
