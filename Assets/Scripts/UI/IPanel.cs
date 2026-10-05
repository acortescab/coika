namespace Coika.UI
{
    /// <summary>
    /// A screen that can be stacked over another one in a <see cref="PanelStack"/>: the pause menu, a confirmation
    /// dialog, and later the Settings screen. It only knows how to appear and disappear; what a panel shows and
    /// which events it raises is its own business.
    /// </summary>
    public interface IPanel
    {
        /// <summary>
        /// Shows the panel.
        /// </summary>
        void Open();

        /// <summary>
        /// Hides the panel.
        /// </summary>
        void Close();
    }
}
