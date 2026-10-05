using System;
using System.Collections.Generic;

namespace Coika.UI
{
    /// <summary>
    /// The panels that are open, bottom to top. Only the top one is acted on: the Back button pops it and leaves
    /// what is under it showing, so the pause menu, the confirmation dialogs and the Settings screen share one
    /// rule (issue #35). It is a plain object and holds no view, so it is tested without a scene.
    /// </summary>
    public sealed class PanelStack
    {
        private readonly List<IPanel> _panels = new List<IPanel>();

        /// <summary>Number of open panels.</summary>
        public int Count => _panels.Count;

        /// <summary>The panel on top, or null when none is open.</summary>
        public IPanel Top => _panels.Count > 0 ? _panels[_panels.Count - 1] : null;

        /// <summary>
        /// Opens a panel over the others. The panels under it stay open, so a dialog is drawn over its menu.
        /// </summary>
        /// <param name="panel">The panel to open.</param>
        /// <exception cref="ArgumentNullException">The panel is null.</exception>
        /// <exception cref="InvalidOperationException">The panel is already in the stack.</exception>
        public void Push(IPanel panel)
        {
            if (panel == null)
            {
                throw new ArgumentNullException(nameof(panel));
            }

            if (_panels.Contains(panel))
            {
                throw new InvalidOperationException("The panel is already open.");
            }

            _panels.Add(panel);
            panel.Open();
        }

        /// <summary>
        /// Closes the panel on top.
        /// </summary>
        /// <returns>The panel that was closed, or null when the stack was empty.</returns>
        public IPanel Pop()
        {
            if (_panels.Count == 0)
            {
                return null;
            }

            var top = _panels[_panels.Count - 1];
            _panels.RemoveAt(_panels.Count - 1);
            top.Close();
            return top;
        }

        /// <summary>
        /// Closes every panel, top first.
        /// </summary>
        public void Clear()
        {
            while (_panels.Count > 0)
            {
                Pop();
            }
        }
    }
}
