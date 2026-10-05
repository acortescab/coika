namespace Coika.Fx
{
    /// <summary>
    /// The visual effects a <see cref="IParticleSpawner"/> can emit (GDD §9).
    /// </summary>
    public enum FxKind
    {
        /// <summary>Pixel particles in the colour of the tier a merge created.</summary>
        MergeBurst,

        /// <summary>A white ring that grows and fades quickly, at a merge.</summary>
        FlashRing,

        /// <summary>A small puff of 3 px sprites where a piece landed hard.</summary>
        LandingDust,

        /// <summary>The big white flash of a supernova.</summary>
        SupernovaFlash,

        /// <summary>The large expanding ring of a supernova.</summary>
        Shockwave,

        /// <summary>Pixel particles that celebrate a new best score.</summary>
        Confetti
    }
}
