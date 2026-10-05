namespace Coika.Gameplay
{
    /// <summary>
    /// Sets of piece effects that exclude each other: starting an effect stops the running effects of its own group.
    /// </summary>
    public enum PieceEffectGroup
    {
        /// <summary>Uniform pops of a new piece: the spawn pop and the merge pop.</summary>
        Pop,

        /// <summary>Squash and stretch caused by the movement of the piece: the fall stretch and the landing squash.</summary>
        Contact,

        /// <summary>Tint flashes, which only change the colour and so do not exclude any other effect.</summary>
        Flash,
    }
}
