namespace Coika.Gameplay
{
    /// <summary>
    /// The substates of the drop while a run is being played (GDD §7).
    /// </summary>
    public enum DropState
    {
        /// <summary>A piece is held and follows the input. It can be released.</summary>
        Aiming,

        /// <summary>A piece was just released and the drop cools down. Input is ignored.</summary>
        Dropping
    }
}
