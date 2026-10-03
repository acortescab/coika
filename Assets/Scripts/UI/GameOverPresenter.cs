using System;
using Coika.Gameplay;
using UnityEngine;

namespace Coika.UI
{
    /// <summary>
    /// Feeds the <see cref="GameOverView"/> with a finished run and forwards its Retry click. It starts no run and
    /// loads no scene: whoever listens to <see cref="RetryRequested"/> (the game manager, issue #11) does that.
    /// </summary>
    public sealed class GameOverPresenter : IDisposable
    {
        private readonly GameOverView _view;
        private readonly Func<int, Sprite> _spriteOf;
        private readonly Action _onRetryClicked;

        /// <summary>
        /// Creates the presenter and subscribes to the Retry button of the view.
        /// </summary>
        /// <param name="view">The view to drive.</param>
        /// <param name="spriteOf">Gives the sprite of a tier, or null when it is not available.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public GameOverPresenter(GameOverView view, Func<int, Sprite> spriteOf)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _spriteOf = spriteOf ?? throw new ArgumentNullException(nameof(spriteOf));

            _onRetryClicked = HandleRetryClicked;
            _view.RetryClicked += _onRetryClicked;
        }

        /// <summary>Raised when the player asks to retry.</summary>
        public event Action RetryRequested;

        /// <summary>
        /// Shows the summary of a finished run.
        /// </summary>
        /// <param name="summary">Snapshot of the run.</param>
        public void Present(RunSummary summary)
        {
            _view.SetHighestTierIcon(_spriteOf(summary.HighestTier));
            _view.Show(summary);
        }

        /// <summary>
        /// Hides the view, for example when a new run starts.
        /// </summary>
        public void Dismiss()
        {
            _view.Hide();
        }

        /// <summary>
        /// Unsubscribes from the view. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            _view.RetryClicked -= _onRetryClicked;
        }

        private void HandleRetryClicked()
        {
            RetryRequested?.Invoke();
        }
    }
}
