using System;
using System.Collections.Generic;
using Coika.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the <see cref="ButtonRelay{T}"/> (issue #35): a click reaches the handler with the value of the button,
    /// and only while the relay is bound.
    /// </summary>
    public class ButtonRelayPlayModeTests
    {
        private GameObject _host;
        private Button _button;

        /// <summary>
        /// Builds a button.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Button", typeof(RectTransform));
            _button = _host.AddComponent<Button>();
        }

        /// <summary>
        /// Destroys the button.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.Destroy(_host);
        }

        /// <summary>
        /// A bound relay passes its value to the handler once per click.
        /// </summary>
        [Test]
        public void Click_WhenBound_PassesTheValueOncePerClick()
        {
            var seen = new List<int>();
            var relay = new ButtonRelay<int>(_button, 7, seen.Add);
            relay.Bind();

            _button.onClick.Invoke();
            _button.onClick.Invoke();

            CollectionAssert.AreEqual(new[] { 7, 7 }, seen);
        }

        /// <summary>
        /// After Unbind a click reaches nobody.
        /// </summary>
        [Test]
        public void Click_AfterUnbind_DoesNothing()
        {
            var seen = new List<int>();
            var relay = new ButtonRelay<int>(_button, 7, seen.Add);
            relay.Bind();
            relay.Unbind();

            _button.onClick.Invoke();

            Assert.That(seen, Is.Empty);
        }

        /// <summary>
        /// A null button or handler is rejected.
        /// </summary>
        [Test]
        public void New_WithANullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ButtonRelay<int>(null, 1, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new ButtonRelay<int>(_button, 1, null));
        }
    }
}
