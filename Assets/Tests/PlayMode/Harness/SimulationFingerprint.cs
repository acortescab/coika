namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// A one-line description of the physics state of a <see cref="SimulationWorld"/>, for the tests that compare two
    /// runs step by step and want to name the first step where they diverge.
    /// </summary>
    public static class SimulationFingerprint
    {
        /// <summary>
        /// Describes the world after a step: the score, the pieces on the board and the sum of their exact positions
        /// and rotations.
        /// </summary>
        /// <param name="world">The world after a step.</param>
        /// <returns>A text that is equal in two runs exactly when their physics are in the same state.</returns>
        public static string Of(SimulationWorld world)
        {
            var x = 0f;
            var y = 0f;
            var angle = 0f;
            var pieces = world.Factory.ActivePieces;
            for (var i = 0; i < pieces.Count; i++)
            {
                var body = pieces[i].Rigidbody;
                x += body.position.x;
                y += body.position.y;
                angle += body.rotation;
            }

            return $"step={world.Steps} score={world.Score.Score} board={world.CountBoardPieces()} sumX={x:R} sumY={y:R} sumAngle={angle:R}";
        }
    }
}
