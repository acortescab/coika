namespace Coika.Core
{
    /// <summary>
    /// The haptic effects the game can play (GDD §9). The order is fixed by the issue and is not the strength order:
    /// <see cref="Haptics"/> ranks the kinds itself.
    /// </summary>
    public enum HapticKind
    {
        /// <summary>A light tap, for example a piece drop.</summary>
        Light,

        /// <summary>The faintest tap, for example a piece landing.</summary>
        VeryLight,

        /// <summary>A medium impact, for example a merge.</summary>
        Medium,

        /// <summary>A strong impact, for example a high tier merge.</summary>
        Heavy,

        /// <summary>The 0.2 s vibration of the game over.</summary>
        Long,
    }
}
