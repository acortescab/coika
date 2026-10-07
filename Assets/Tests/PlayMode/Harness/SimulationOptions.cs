using System.Collections.Generic;
using Coika.Core;
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

        /// <summary>Mode of the runs. Classic, like the shipped game, unless a test bends it.</summary>
        public GameMode Mode { get; set; } = GameMode.Classic;

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

        /// <summary>
        /// Whether the visual piece animations (the animators and the merge ghosts) run. They are on by default, like in
        /// the shipped game; a test turns them off to prove they never change the physics.
        /// </summary>
        public bool Animations { get; set; } = true;

        /// <summary>
        /// Whether the pooled particles (issue #32) run. They are on by default, like in the shipped game; a test
        /// turns them off to prove they never change the physics or the score.
        /// </summary>
        public bool Particles { get; set; } = true;

        /// <summary>
        /// Whether the <see cref="Coika.Fx.FeedbackDirector"/> (issue #34) is bound, with fake audio, haptics and screen
        /// effects. It is on by default, like in the shipped game; a test turns it off to prove it never changes the
        /// simulation.
        /// </summary>
        public bool Feedback { get; set; } = true;

        /// <summary>
        /// Whether the slow-mo of the director is applied to a real <see cref="Coika.Fx.TimeScaleOwner"/> on a recording
        /// time scale, besides being recorded. Off by default; it needs <see cref="Feedback"/>. A test turns it on to
        /// prove the slow-mo never changes the simulation.
        /// </summary>
        public bool SlowMo { get; set; }

        /// <summary>
        /// Indexes of the drops (in the list given to one play call) after which the runner pauses the run for
        /// <see cref="PauseSteps"/> attempted steps and resumes it. Null or empty for no pause.
        /// </summary>
        public IReadOnlyList<int> PauseAfterDrops { get; set; }

        /// <summary>Steps attempted while paused at each pause. Nothing advances during them.</summary>
        public int PauseSteps { get; set; } = 30;

        /// <summary>Overrides the cap of live pixel particles, so a test can reach it. Null keeps the config default.</summary>
        public int? ParticleCap { get; set; }

        /// <summary>Overrides the cap of live rings, so a test can reach it. Null keeps the config default.</summary>
        public int? RingCap { get; set; }
    }
}
