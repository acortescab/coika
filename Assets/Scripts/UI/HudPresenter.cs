using System;
using Coika.Gameplay;
using UnityEngine;

namespace Coika.UI
{
    /// <summary>
    /// Connects the gameplay events to the <see cref="HudView"/>: the score, the combo multiplier and the next
    /// piece. All the logic of the HUD lives here, so the view stays passive (S-91). Handlers are cached delegates
    /// and the view allocates nothing, so showing an update costs no garbage (S-50).
    /// <para>
    /// A run creates a new <see cref="SpawnQueue"/>, so the queue is attached with <see cref="BindQueue"/> and can
    /// be replaced; the score system lives across runs. <see cref="Refresh"/> re-reads everything for a new run,
    /// because <see cref="ScoreSystem.ResetForNewRun"/> raises no events.
    /// </para>
    /// </summary>
    public sealed class HudPresenter : IDisposable
    {
        private readonly HudView _view;
        private readonly ScoreSystem _score;
        private readonly Func<int, Sprite> _spriteOf;
        private readonly Action<int, int> _onScoreChanged;
        private readonly Action<int, float> _onComboChanged;
        private readonly Action _onQueueAdvanced;

        private SpawnQueue _queue;

        /// <summary>
        /// Creates the presenter, subscribes to the score system and shows the current values.
        /// </summary>
        /// <param name="view">The view to drive.</param>
        /// <param name="score">Source of the score, the best score and the combo.</param>
        /// <param name="spriteOf">Gives the sprite of a tier, or null when it is not available.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public HudPresenter(HudView view, ScoreSystem score, Func<int, Sprite> spriteOf)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _score = score ?? throw new ArgumentNullException(nameof(score));
            _spriteOf = spriteOf ?? throw new ArgumentNullException(nameof(spriteOf));

            _onScoreChanged = HandleScoreChanged;
            _onComboChanged = HandleComboChanged;
            _onQueueAdvanced = ShowNext;

            _score.ScoreChanged += _onScoreChanged;
            _score.ComboTracker.ComboChanged += _onComboChanged;
            Refresh();
        }

        /// <summary>
        /// Starts following a queue, replacing the previous one, and shows its next piece. A null queue only
        /// detaches.
        /// </summary>
        /// <param name="queue">Queue of the current run, or null.</param>
        public void BindQueue(SpawnQueue queue)
        {
            if (_queue != null)
            {
                _queue.Advanced -= _onQueueAdvanced;
            }

            _queue = queue;
            if (_queue != null)
            {
                _queue.Advanced += _onQueueAdvanced;
            }

            ShowNext();
        }

        /// <summary>
        /// Shows the score, the best score, the combo and the next piece as they are now.
        /// </summary>
        public void Refresh()
        {
            _view.SetScore(_score.Score);
            _view.SetBest(_score.BestScore);
            _view.SetCombo(_score.ComboTracker.Multiplier);
            ShowNext();
        }

        /// <summary>
        /// Unsubscribes from everything. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            _score.ScoreChanged -= _onScoreChanged;
            _score.ComboTracker.ComboChanged -= _onComboChanged;
            if (_queue != null)
            {
                _queue.Advanced -= _onQueueAdvanced;
                _queue = null;
            }
        }

        /// <summary>
        /// Shows the new score.
        /// </summary>
        private void HandleScoreChanged(int score, int delta)
        {
            _view.SetScore(score);
        }

        /// <summary>
        /// Shows the new combo multiplier, or hides it at x1.
        /// </summary>
        private void HandleComboChanged(int combo, float multiplier)
        {
            _view.SetCombo(multiplier);
        }

        /// <summary>
        /// Shows the sprite of the tier the queue will drop next, or nothing when no queue is bound.
        /// </summary>
        private void ShowNext()
        {
            _view.SetNextPreview(_queue != null ? _spriteOf(_queue.Next) : null);
        }
    }
}
