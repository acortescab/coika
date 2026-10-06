namespace Coika.Core
{
    /// <summary>
    /// Implemented by a scene root that adapts to the device tier. The Boot installer hands the tier over right after
    /// the scene loads, next to <see cref="IHapticsConsumer"/>, so scenes never look it up and there is no static instance.
    /// </summary>
    public interface IQualityConsumer
    {
        /// <summary>
        /// Receives the device tier, before the consumer builds its objects.
        /// </summary>
        /// <param name="quality">The tier owned by the Boot installer.</param>
        void UseQuality(IQualityTier quality);
    }
}
