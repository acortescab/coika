namespace Coika.Gameplay
{
    /// <summary>
    /// Identifies a visual effect of <see cref="PieceAnimator"/>. The value is the index of the effect in the
    /// animator, so a new effect needs a new value here and its class in <c>PieceAnimator.CreateEffects</c>.
    /// </summary>
    public enum PieceEffectId
    {
        /// <summary>Scale from 0 to 1 with overshoot when a piece appears.</summary>
        Spawn,

        /// <summary>Pop of a piece created by a merge, from 0 up to a peak and back to 1.</summary>
        MergePop,

        /// <summary>Vertical stretch of a falling piece.</summary>
        Drop,

        /// <summary>Squash and stretch when a piece lands.</summary>
        Land,

        /// <summary>Delayed tint flash of a piece at game over; the parameter is the delay in seconds.</summary>
        GameOverFlash,
    }
}
