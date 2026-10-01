using System;
using Coika.Data;

namespace Coika.Gameplay
{
    /// <summary>
    /// Decides which tier is dropped next. The player never chooses: the queue holds the tier being dropped
    /// (<see cref="Current"/>) and the one after it (<see cref="Next"/>, shown in the preview), and
    /// <see cref="Advance"/> moves on one piece.
    /// <para>
    /// Rules (GDD §3.3): only the spawnable tiers appear, chosen at random by their weights; the same tier never
    /// appears more than the anti-streak maximum times in a row; and the first pieces of a run follow the forced
    /// opening, which counts toward that streak and is never re-rolled.
    /// </para>
    /// <para>
    /// It is deterministic: the same seed and settings always give the same sequence. It uses its own
    /// <see cref="Random"/> and never UnityEngine.Random, and it allocates nothing in <see cref="Advance"/>.
    /// </para>
    /// <para>
    /// Seed policy: the queue never picks a seed. The game manager creates one queue per run with a seed (a
    /// time-based one in Classic, the date in Daily) and keeps it in the run state; there is no reset, a new run is
    /// a new queue. <see cref="GetState"/> and <see cref="SetState"/> let a run be resumed later.
    /// </para>
    /// </summary>
    public sealed class SpawnQueue
    {
        /// <summary>
        /// Most advances <see cref="SetState"/> replays. It is far above any real run and stops a corrupt save from
        /// freezing the game in a replay that never ends.
        /// </summary>
        public const int MAX_ADVANCE_COUNT = 1_000_000;

        private readonly SpawnSettings _settings;

        private Random _random;
        private int _generated;
        private int _lastTier;
        private int _streak;
        private int _advanceCount;

        /// <summary>
        /// Creates a queue from the spawn values of a config. A new queue starts a new run.
        /// </summary>
        /// <param name="config">Source of the spawn values. They are copied, so the config can change afterwards.</param>
        /// <param name="seed">Seed of the run.</param>
        /// <exception cref="ArgumentNullException">The config is null.</exception>
        /// <exception cref="ArgumentException">The config holds invalid spawn values.</exception>
        public SpawnQueue(GameConfig config, int seed)
            : this(CreateSettings(config), seed)
        {
        }

        /// <summary>
        /// Creates a queue from ready settings. <see cref="Current"/> and <see cref="Next"/> are filled at once.
        /// </summary>
        /// <param name="settings">The spawn values.</param>
        /// <param name="seed">Seed of the run.</param>
        /// <exception cref="ArgumentNullException">The settings are null.</exception>
        public SpawnQueue(SpawnSettings settings, int seed)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Begin(seed);
        }

        /// <summary>
        /// Raised after <see cref="Advance"/> and <see cref="SetState"/>, once the new <see cref="Current"/> and
        /// <see cref="Next"/> are in place, so a preview can refresh. It is not raised by the constructor.
        /// </summary>
        public event Action Advanced;

        /// <summary>Seed of the run.</summary>
        public int Seed { get; private set; }

        /// <summary>Tier of the piece being dropped.</summary>
        public int Current { get; private set; }

        /// <summary>Tier of the piece that comes after the current one, for the preview.</summary>
        public int Next { get; private set; }

        /// <summary>
        /// Moves on one piece: the next tier becomes the current one and a new next tier is generated. It draws
        /// at most one random number and allocates nothing.
        /// </summary>
        /// <returns>The new current tier.</returns>
        public int Advance()
        {
            Step();
            Advanced?.Invoke();
            return Current;
        }

        /// <summary>
        /// Captures what is needed to resume the run: the seed and how many times the queue advanced. It allocates,
        /// so call it when saving, not every frame.
        /// </summary>
        /// <returns>The state.</returns>
        public SpawnQueueState GetState()
        {
            return new SpawnQueueState { Version = SpawnQueueState.CURRENT_VERSION, Seed = Seed, AdvanceCount = _advanceCount };
        }

        /// <summary>
        /// Resumes a run: it starts again from the seed of the state and replays the advances, so the sequence
        /// continues exactly as it would have. The time it takes grows with the number of advances. It raises
        /// <see cref="Advanced"/> once at the end, so a preview refreshes. The state of the queue does not change
        /// when the given state is rejected.
        /// </summary>
        /// <param name="state">A state taken with <see cref="GetState"/>, for the same settings.</param>
        /// <exception cref="ArgumentNullException">The state is null.</exception>
        /// <exception cref="ArgumentException">The state has an unknown version.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The advance count is negative or above <see cref="MAX_ADVANCE_COUNT"/>.</exception>
        public void SetState(SpawnQueueState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.Version != SpawnQueueState.CURRENT_VERSION)
            {
                throw new ArgumentException($"Unknown spawn queue state version {state.Version}.", nameof(state));
            }

            if (state.AdvanceCount < 0 || state.AdvanceCount > MAX_ADVANCE_COUNT)
            {
                throw new ArgumentOutOfRangeException(nameof(state), $"The advance count must be between 0 and {MAX_ADVANCE_COUNT}, got {state.AdvanceCount}.");
            }

            Begin(state.Seed);
            for (int i = 0; i < state.AdvanceCount; i++)
            {
                Step();
            }

            Advanced?.Invoke();
        }

        /// <summary>
        /// Takes the spawn values out of a config, failing clearly when the config is missing.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <returns>The validated spawn settings.</returns>
        /// <exception cref="ArgumentNullException">The config is null.</exception>
        private static SpawnSettings CreateSettings(GameConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return config.CreateSpawnSettings();
        }

        /// <summary>
        /// Starts the sequence from a seed: a new random generator, no streak, and the first two tiers.
        /// </summary>
        /// <param name="seed">Seed of the run.</param>
        private void Begin(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
            _generated = 0;
            _lastTier = SpawnSelector.NO_TIER;
            _streak = 0;
            _advanceCount = 0;

            Current = Generate();
            Next = Generate();
        }

        /// <summary>
        /// Shifts the queue one piece without raising the event, so <see cref="SetState"/> can replay many.
        /// </summary>
        private void Step()
        {
            Current = Next;
            Next = Generate();
            _advanceCount++;
        }

        /// <summary>
        /// Produces the tier of the next piece. The first pieces come from the forced opening and draw no random
        /// number; the rest are drawn once, leaving out the tier whose streak is already at the maximum. Every tier,
        /// forced or not, updates the streak, so the opening counts toward it.
        /// </summary>
        /// <returns>The tier.</returns>
        private int Generate()
        {
            int tier;
            if (_generated < _settings.ForcedOpeningLength)
            {
                tier = _settings.GetForcedTier(_generated);
            }
            else
            {
                var excludedTier = _streak >= _settings.AntiStreakMax ? _lastTier : SpawnSelector.NO_TIER;
                tier = SpawnSelector.Pick(_settings, excludedTier, _random.NextDouble());
            }

            _streak = tier == _lastTier ? _streak + 1 : 1;
            _lastTier = tier;
            _generated++;
            return tier;
        }
    }
}
