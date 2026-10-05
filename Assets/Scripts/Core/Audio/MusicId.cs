namespace Coika.Core
{
    /// <summary>
    /// The music tracks of the game (GDD §11). The name of an enum value is also the name of its clip.
    /// </summary>
    public enum MusicId
    {
        /// <summary>Loop that plays during a run.</summary>
        Gameplay,
        /// <summary>Loop reserved for the menu (M3).</summary>
        Menu
    }
}
