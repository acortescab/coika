using System;

namespace Coika.Gameplay
{
    /// <summary>
    /// What is needed to resume a <see cref="SpawnQueue"/>: its seed and how many times it advanced. The queue
    /// replays those advances, so nothing else has to be saved. It is a plain serializable class with a version
    /// field and no references to Unity objects (S-33), so it can go into the run state and the save file.
    /// </summary>
    [Serializable]
    public sealed class SpawnQueueState
    {
        /// <summary>Version of this layout. A state with another version is rejected when it is restored.</summary>
        public const int CURRENT_VERSION = 1;

        /// <summary>Version of the layout this state was saved with.</summary>
        public int Version = CURRENT_VERSION;

        /// <summary>Seed the run was started with.</summary>
        public int Seed;

        /// <summary>Number of times the queue advanced since the start of the run.</summary>
        public int AdvanceCount;
    }
}
