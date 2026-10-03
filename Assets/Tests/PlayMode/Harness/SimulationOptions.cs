using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// What a simulated run needs besides its drop X positions: the seed and the few rules a test may bend (a
    /// smaller jar, a shorter cooldown, a forced opening), so a test states exactly what it changes. Every
    /// property that is left alone keeps the value of the shipped game.
    /// </summary>
    public sealed class SimulationOptions
    {
        /// <summary>Seed of the first run. Each restart after a game over uses the next seed.</summary>
        public int Seed { get; set; } = 1;

        /// <summary>Tiers of the first pieces, in order, replacing the default opening 0, 1, 0. Null keeps the default.</summary>
        public int[] ForcedOpening { get; set; }

        /// <summary>Interior size of the jar in world units. Null keeps the 10 x 12.5 of the GDD.</summary>
        public Vector2? JarSize { get; set; }

        /// <summary>Seconds the next drop is blocked after a drop. Null keeps the 0.5 s of the GDD.</summary>
        public float? DropCooldown { get; set; }

        /// <summary>Least seconds between two drops, on top of the cooldown. It lets a test wait for the board to calm down.</summary>
        public float MinSecondsBetweenDrops { get; set; }

        /// <summary>Seconds simulated after the last drop, or until the game is over.</summary>
        public float SettleSeconds { get; set; }

        /// <summary>
        /// Whether a game over starts a new run (through Retry) and the simulation goes on, so long scripts never run
        /// out of jar. When false the simulation ends at the first game over.
        /// </summary>
        public bool RestartOnGameOver { get; set; }
    }
}
