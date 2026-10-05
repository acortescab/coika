namespace Coika.Core
{
    /// <summary>
    /// Every sound effect of the game (GDD §11). The name of an enum value is also the name of its clip; clips of a
    /// value with several variants end in a number (Land1, Land2, Land3).
    /// </summary>
    public enum SfxId
    {
        /// <summary>A new piece appears over the jar.</summary>
        Spawn,
        /// <summary>The player releases the piece.</summary>
        Drop,
        /// <summary>A piece hits the jar or another piece. Has 3 variants.</summary>
        Land,
        /// <summary>Two pieces merge. One sample, pitched per tier.</summary>
        Merge,
        /// <summary>A merge into a high tier (8 or more).</summary>
        MergeBig,
        /// <summary>The biggest merge.</summary>
        Supernova,
        /// <summary>The overflow warning, 4 times per second.</summary>
        DangerTick,
        /// <summary>The run ends.</summary>
        GameOver,
        /// <summary>The player beats the best score.</summary>
        NewBest,
        /// <summary>A UI button is pressed.</summary>
        UiClick,
        /// <summary>A UI back action.</summary>
        UiBack
    }
}
