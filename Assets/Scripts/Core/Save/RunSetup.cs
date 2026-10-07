namespace Coika.Core
{
    /// <summary>
    /// The mode of the next run and the seed of the run in progress. The installer creates it and the Menu or Modes
    /// screen writes it; the Game scene reads it when it starts. It travels with the installer's services, never
    /// through statics. A new setup is Classic with no seed.
    /// </summary>
    public sealed class RunSetup
    {
        /// <summary>Mode of the runs, Classic until another is chosen.</summary>
        public GameMode Mode { get; private set; } = GameMode.Classic;

        /// <summary>Seed of the last run started, valid only when <see cref="HasSeed"/> is true.</summary>
        public int Seed { get; private set; }

        /// <summary>Whether a run started since the mode was chosen, so a retry can replay its seed.</summary>
        public bool HasSeed { get; private set; }

        /// <summary>
        /// Chooses the mode of the next runs and forgets the seed of the previous one.
        /// </summary>
        /// <param name="mode">The mode.</param>
        public void Choose(GameMode mode)
        {
            Mode = mode;
            HasSeed = false;
        }

        /// <summary>
        /// Remembers the seed of the run that just started.
        /// </summary>
        /// <param name="seed">The seed.</param>
        public void Remember(int seed)
        {
            Seed = seed;
            HasSeed = true;
        }
    }
}
