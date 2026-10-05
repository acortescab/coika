using System;
using Coika.UI;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the <see cref="PanelStack"/> (issue #35): panels open over each other and close top first.
    /// </summary>
    public class PanelStackTests
    {
        private PanelStack _stack;
        private FakePanel _first;
        private FakePanel _second;

        /// <summary>
        /// Builds an empty stack and two panels.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _stack = new PanelStack();
            _first = new FakePanel();
            _second = new FakePanel();
        }

        /// <summary>
        /// A new stack has no panel.
        /// </summary>
        [Test]
        public void New_Always_IsEmpty()
        {
            Assert.AreEqual(0, _stack.Count);
            Assert.IsNull(_stack.Top);
        }

        /// <summary>
        /// Pushing opens the panel and makes it the top, and leaves the one under it open.
        /// </summary>
        [Test]
        public void Push_TwoPanels_OpensBothAndTopsTheSecond()
        {
            _stack.Push(_first);
            _stack.Push(_second);

            Assert.AreEqual(2, _stack.Count);
            Assert.AreSame(_second, _stack.Top);
            Assert.AreEqual(1, _first.Opened);
            Assert.AreEqual(0, _first.Closed);
            Assert.AreEqual(1, _second.Opened);
        }

        /// <summary>
        /// Pop closes the top and shows the panel under it again as the top.
        /// </summary>
        [Test]
        public void Pop_WithTwoPanels_ClosesOnlyTheTop()
        {
            _stack.Push(_first);
            _stack.Push(_second);

            var popped = _stack.Pop();

            Assert.AreSame(_second, popped);
            Assert.AreEqual(1, _second.Closed);
            Assert.AreEqual(0, _first.Closed);
            Assert.AreSame(_first, _stack.Top);
        }

        /// <summary>
        /// Pop on an empty stack returns null and changes nothing.
        /// </summary>
        [Test]
        public void Pop_WhenEmpty_ReturnsNull()
        {
            Assert.IsNull(_stack.Pop());
        }

        /// <summary>
        /// Clear closes every panel and empties the stack.
        /// </summary>
        [Test]
        public void Clear_WithTwoPanels_ClosesAllOfThem()
        {
            _stack.Push(_first);
            _stack.Push(_second);

            _stack.Clear();

            Assert.AreEqual(0, _stack.Count);
            Assert.AreEqual(1, _first.Closed);
            Assert.AreEqual(1, _second.Closed);
        }

        /// <summary>
        /// The same panel cannot be open twice.
        /// </summary>
        [Test]
        public void Push_SamePanelTwice_Throws()
        {
            _stack.Push(_first);

            Assert.Throws<InvalidOperationException>(() => _stack.Push(_first));
        }

        /// <summary>
        /// A null panel is rejected.
        /// </summary>
        [Test]
        public void Push_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _stack.Push(null));
        }

        /// <summary>
        /// A panel that counts how often it was opened and closed.
        /// </summary>
        private sealed class FakePanel : IPanel
        {
            /// <summary>Number of calls to <see cref="Open"/>.</summary>
            public int Opened { get; private set; }

            /// <summary>Number of calls to <see cref="Close"/>.</summary>
            public int Closed { get; private set; }

            /// <inheritdoc />
            public void Open()
            {
                Opened++;
            }

            /// <inheritdoc />
            public void Close()
            {
                Closed++;
            }
        }
    }
}
