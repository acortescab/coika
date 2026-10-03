using System;
using Coika.Data;

namespace Coika.Gameplay
{
    /// <summary>
    /// Counts the merges that follow each other closely and turns the count into the score multiplier (GDD §4). A
    /// merge within the combo window of the previous one raises the combo by one; a later one starts it again at 1;
    /// and <see cref="Tick"/> drops it to 0 when the window passes without a merge.
    /// <para>
    /// It is plain C# and never reads the clock: every method takes the time it is called at, in seconds, so the same
    /// calls always give the same combo (S-63). In the game the owner passes <c>Time.timeAsDouble</c>. The window, the
    /// step and the cap come from the <see cref="GameConfig"/> (S-30). It allocates nothing.
    /// </para>
    /// </summary>
    public sealed class ComboTracker
    {
        private readonly double _window;
        private readonly float _step;
        private readonly float _cap;

        private double _lastMergeTime;

        /// <summary>
        /// Creates a tracker with the combo values of a config. The values are copied, so the config can change
        /// afterwards. The combo starts at 0.
        /// </summary>
        /// <param name="config">Source of the combo window, step and cap.</param>
        /// <exception cref="ArgumentNullException">The config is null.</exception>
        public ComboTracker(GameConfig config)
        {
            var source = config != null ? config : throw new ArgumentNullException(nameof(config));
            _window = source.ComboWindow;
            _step = source.ComboStep;
            _cap = source.ComboCap;
        }

        /// <summary>
        /// Raised with the new combo and its multiplier after a merge and when the combo expires (combo 0, ×1). It
        /// is not raised by <see cref="Reset"/>.
        /// </summary>
        public event Action<int, float> ComboChanged;

        /// <summary>Merges in the current combo: 0 when there is none, 1 after the first merge.</summary>
        public int Combo { get; private set; }

        /// <summary>
        /// The score multiplier of the current combo: <c>min(1 + step × (combo − 1), cap)</c>, and 1 at combo 0 or 1.
        /// </summary>
        public float Multiplier => Combo <= 1 ? 1f : Math.Min(1f + _step * (Combo - 1), _cap);

        /// <summary>
        /// Registers a merge. It continues the combo when it is within the window of the previous merge, and starts
        /// it again at 1 otherwise.
        /// </summary>
        /// <param name="time">Time of the merge, in seconds.</param>
        public void RegisterMerge(double time)
        {
            Combo = Combo > 0 && IsWithinWindow(time) ? Combo + 1 : 1;
            _lastMergeTime = time;
            ComboChanged?.Invoke(Combo, Multiplier);
        }

        /// <summary>
        /// Ends the combo when the window has passed without a merge. The owner calls it every frame; it does
        /// nothing while there is no combo or the window is still open.
        /// </summary>
        /// <param name="time">The current time, in seconds.</param>
        public void Tick(double time)
        {
            if (Combo == 0 || IsWithinWindow(time))
            {
                return;
            }

            Combo = 0;
            ComboChanged?.Invoke(0, 1f);
        }

        /// <summary>
        /// Forgets the combo for a new run. It raises no event: whoever shows the combo reads it when the run starts.
        /// The time of the last merge needs no reset, because it is only read while there is a combo.
        /// </summary>
        public void Reset()
        {
            Combo = 0;
        }

        /// <summary>
        /// Whether a time is still inside the window that opened at the last merge. The window is inclusive: a
        /// merge exactly one window later continues the combo.
        /// </summary>
        private bool IsWithinWindow(double time)
        {
            return time - _lastMergeTime <= _window;
        }
    }
}
