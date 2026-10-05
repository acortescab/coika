namespace Coika.UI
{
    /// <summary>
    /// A connection between a control and a handler that a view binds while it is on screen: <c>Bind</c> in
    /// <c>OnEnable</c> and <c>Unbind</c> in <c>OnDisable</c> (S-23). A view keeps its relays in one list and loops
    /// over it, whatever the controls are.
    /// </summary>
    public interface IRelay
    {
        /// <summary>
        /// Starts listening to the control.
        /// </summary>
        void Bind();

        /// <summary>
        /// Stops listening to the control.
        /// </summary>
        void Unbind();
    }
}
