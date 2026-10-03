using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Base of the simulation tests: it records every warning the test logs and fails the test if there was any
    /// (issue #12: 0 console warnings during test runs). Errors and exceptions already fail a Unity test by themselves.
    /// A test that expects a warning must not derive from this class.
    /// </summary>
    public abstract class HarnessTestBase
    {
        private readonly List<string> _warnings = new();

        /// <summary>
        /// Starts recording warnings.
        /// </summary>
        [SetUp]
        public void StartRecordingWarnings()
        {
            _warnings.Clear();
            Application.logMessageReceived += OnLogMessage;
        }

        /// <summary>
        /// Stops recording and fails when a warning was logged.
        /// </summary>
        [TearDown]
        public void FailOnWarnings()
        {
            Application.logMessageReceived -= OnLogMessage;
            Assert.IsEmpty(_warnings, "The test logged warnings:\n" + string.Join("\n", _warnings));
        }

        /// <summary>
        /// Keeps the warnings.
        /// </summary>
        /// <param name="condition">Text of the message.</param>
        /// <param name="stackTrace">Stack trace of the message, not kept.</param>
        /// <param name="type">Kind of message.</param>
        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
            {
                _warnings.Add(condition);
            }
        }
    }
}
