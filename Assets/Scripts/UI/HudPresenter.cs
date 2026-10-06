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
    /// <para>
    /// The score rolls to its new value, the combo label punches on each increment and the next-piece preview pops
    /// when the tier changes (issue #37). They run on the injected unscaled clock from <see cref="Tick"/>;
    /// <see cref="SkipAnimations"/> ends them at once, for a paused or finished run. Once the run beats the best
    /// score, the best label follows the score.
    /// </para>
    /// </summary>
    public sealed class HudPresenter : IDisposable
    {
        private const int NO_TIER = -1;

        private readonly HudView _view;
        private readonly ScoreSystem _score;
        private readonly Func<int, Sprite> _spriteOf;
        private readonly CountUp _scoreCount;
        private readonly Punch _comboPunch;
        private readonly Punch _nextPop;
        private readonly TimedAnimation[] _animations;
        private readonly Action<int, int> _onScoreChanged;
        private readonly Action<int, float> _onComboChanged;
        private readonly Action _onNewBest;
        private readonly Action _onQueueAdvanced;

        private SpawnQueue _queue;
        private int _lastCombo;
        private int _shownTier = NO_TIER;
        private bool _bestFollowsScore;

        /// <summary>
        /// Creates the presenter, subscribes to the score system and shows the current values.
        /// </summary>
        /// <param name="view">The view to drive.</param>
        /// <param name="score">Source of the score, the best score and the combo.</param>
        /// <param name="spriteOf">Gives the sprite of a tier, or null when it is not available.</param>
        /// <param name="clock">Unscaled time source in seconds, for the animations.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public HudPresenter(HudView view, ScoreSystem score, Func<int, Sprite> spriteOf, Func<double> clock)
        {
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));
            _score = score ?? throw new ArgumentNullException(nameof(score));
            _spriteOf = spriteOf ?? throw new ArgumentNullException(nameof(spriteOf));
            _scoreCount = new CountUp(clock, UiAnimation.COUNT_UP_SECONDS);
            _comboPunch = new Punch(clock, UiAnimation.COMBO_PUNCH_SECONDS, UiAnimation.COMBO_PUNCH_SCALE);
            _nextPop = new Punch(clock, UiAnimation.NEXT_POP_SECONDS, UiAnimation.NEXT_POP_SCALE);
            _animations = new TimedAnimation[] { _scoreCount, _comboPunch, _nextPop };

            _onScoreChanged = HandleScoreChanged;
            _onComboChanged = HandleComboChanged;
            _onNewBest = HandleNewBest;
            _onQueueAdvanced = HandleQueueAdvanced;

            _score.ScoreChanged += _onScoreChanged;
            _score.ComboTracker.ComboChanged += _onComboChanged;
            _score.NewBestReached += _onNewBest;
            Refresh();
        }

        /// <summary>
        /// Scales the animation durations, for reduced motion (<see cref="UiAnimation.MotionScale"/>).
        /// </summary>
        /// <param name="scale">1 for normal motion, less to shorten the animations.</param>
        public void SetMotionScale(float scale)
        {
            for (var i = 0; i < _animations.Length; i++)
            {
                _animations[i].SetScale(scale);
            }
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

            ShowNext(false);
        }

        /// <summary>
        /// Shows the score, the best score, the combo and the next piece as they are now, with no animation.
        /// </summary>
        public void Refresh()
        {
            _bestFollowsScore = _score.IsNewBest;
            _lastCombo = _score.ComboTracker.Combo;
            _view.SetCombo(_score.ComboTracker.Multiplier);
            ShowNext(false);
            SkipAnimations();
        }

        /// <summary>
        /// Advances the animations to the current time. Call it once per frame.
        /// </summary>
        public void Tick()
        {
            if (_scoreCount.Tick())
            {
                ShowScore();
            }

            if (_comboPunch.Running)
            {
                _view.SetComboScale(_comboPunch.Evaluate());
            }

            if (_nextPop.Running)
            {
                _view.SetNextScale(_nextPop.Evaluate());
            }
        }

        /// <summary>
        /// Ends every animation at once: the score shows its exact value and the scales go back to rest. Used when
        /// the run is paused or over.
        /// </summary>
        public void SkipAnimations()
        {
            _scoreCount.Snap(_score.Score);
            ShowScore();
            _comboPunch.Cancel();
            _nextPop.Cancel();
            _view.SetComboScale(1f);
            _view.SetNextScale(1f);
        }

        /// <summary>
        /// Unsubscribes from everything. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            _score.ScoreChanged -= _onScoreChanged;
            _score.ComboTracker.ComboChanged -= _onComboChanged;
            _score.NewBestReached -= _onNewBest;
            if (_queue != null)
            {
                _queue.Advanced -= _onQueueAdvanced;
                _queue = null;
            }
        }

        /// <summary>
        /// Shows the rolling score, and the best label too once the run has beaten the best score.
        /// </summary>
        private void ShowScore()
        {
            _view.SetScore(_scoreCount.Value);
            _view.SetBest(_bestFollowsScore ? _scoreCount.Value : _score.BestScore);
        }

        /// <summary>
        /// Starts rolling to the new score.
        /// </summary>
        private void HandleScoreChanged(int score, int delta)
        {
            _scoreCount.SetTarget(score);
            if (!_scoreCount.Running)
            {
                ShowScore();
            }
        }

        /// <summary>
        /// Makes the best label follow the score from now on, so it updates the moment the best is beaten.
        /// </summary>
        private void HandleNewBest()
        {
            _bestFollowsScore = true;
            ShowScore();
        }

        /// <summary>
        /// Shows the new combo multiplier, or hides it at x1, and punches the label when the combo grew.
        /// </summary>
        private void HandleComboChanged(int combo, float multiplier)
        {
            _view.SetCombo(multiplier);
            if (combo > _lastCombo && multiplier > 1f)
            {
                _comboPunch.Start();
            }

            _lastCombo = combo;
        }

        /// <summary>
        /// Shows the next piece, popping the preview when its tier changed.
        /// </summary>
        private void HandleQueueAdvanced()
        {
            ShowNext(true);
        }

        /// <summary>
        /// Shows the sprite of the tier the queue will drop next, or nothing when no queue is bound.
        /// </summary>
        /// <param name="pop">True to pop the preview when the tier differs from the one shown.</param>
        private void ShowNext(bool pop)
        {
            var tier = _queue != null ? _queue.Next : NO_TIER;
            _view.SetNextPreview(_queue != null ? _spriteOf(tier) : null);
            if (pop && tier != _shownTier)
            {
                _nextPop.Start();
            }

            _shownTier = tier;
        }
    }
}
