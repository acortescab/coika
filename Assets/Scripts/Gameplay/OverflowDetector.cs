using System;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The only way to lose (GDD §3.6): a piece that stays settled above the Danger Line long enough. The rule is
    /// meant to be fair, so a piece that merely passes above the line while it falls or bounces never counts. A piece
    /// is <b>overflowing</b> when all of these are true: the top of its collider is above the line, it is settled,
    /// it is not held, and it is older than the overflow grace (which also covers the pieces a merge creates, through
    /// their spawn grace). A piece that stops overflowing starts from 0 again; when any piece has overflowed for the
    /// overflow time without a break, <see cref="GameOverTriggered"/> is raised once and the detector stops
    /// evaluating until <see cref="ResetForNewRun"/>.
    /// <para>
    /// It evaluates at 10 Hz, not every physics step: <see cref="FixedUpdate"/> accumulates the fixed time and calls
    /// <see cref="Evaluate"/> once per interval. The time of each piece lives on the <see cref="Piece"/>, so a piece
    /// that is released leaves nothing behind and the evaluation allocates nothing (S-50).
    /// </para>
    /// <para>
    /// It also drives the <see cref="DangerLine"/>: visible while a piece that is not held is within the warning
    /// distance of the line, pulsing while a piece is overflowing. It shows no game over screen and changes no game
    /// state (issues #10 and #11). It receives everything through <see cref="Initialize"/> (S-22).
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/Overflow Detector")]
    [DisallowMultipleComponent]
    public class OverflowDetector : MonoBehaviour
    {
        /// <summary>Seconds between evaluations (10 Hz).</summary>
        public const float EVALUATION_INTERVAL = 0.1f;

        // Slack for the sums of fixed steps and of intervals, which do not add up to exact floats.
        private const float TIME_TOLERANCE = 0.001f;

        private PieceFactory _factory;
        private Jar _jar;
        private float _overflowTime;
        private float _overflowGrace;
        private float _warnDistance;
        private float _accumulator;
        private bool _active;
        private bool _triggered;

        /// <summary>Raised once per run, when a piece has overflowed for the overflow time.</summary>
        public event Action GameOverTriggered;

        /// <summary>Raised with the new <see cref="WorstOverflowProgress"/> every time it changes, for the HUD and the audio.</summary>
        public event Action<float> OverflowProgressChanged;

        /// <summary>Overflow of the worst piece as a fraction of the overflow time, from 0 to 1.</summary>
        public float WorstOverflowProgress { get; private set; }

        /// <summary>Whether the detector is running. While it is not, nothing advances.</summary>
        public bool IsRunning => _active;

        /// <summary>Whether <see cref="GameOverTriggered"/> was raised in this run.</summary>
        public bool HasTriggered => _triggered;

        /// <summary>
        /// Gives the detector what it works with. Call it once, before <see cref="Enable"/>. It copies the overflow
        /// values of the config, so the config can change afterwards.
        /// </summary>
        /// <param name="factory">Source of the pieces in play.</param>
        /// <param name="jar">Gives the Danger Line height and the line to show and pulse.</param>
        /// <param name="config">Source of the overflow time, the grace and the warning distance.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public void Initialize(PieceFactory factory, Jar jar, GameConfig config)
        {
            var newFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            var newJar = jar != null ? jar : throw new ArgumentNullException(nameof(jar));
            var source = config != null ? config : throw new ArgumentNullException(nameof(config));

            _factory = newFactory;
            _jar = newJar;
            _overflowTime = source.OverflowTime;
            _overflowGrace = source.OverflowGrace;
            _warnDistance = source.DangerWarnDistance;
            ClearState();
        }

        /// <summary>
        /// Starts evaluating, or resumes after <see cref="Disable"/> with the times of the pieces as they were.
        /// </summary>
        /// <exception cref="InvalidOperationException">The detector was not initialized.</exception>
        public void Enable()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException("Initialize the OverflowDetector before enabling it.");
            }

            _active = true;
        }

        /// <summary>
        /// Stops evaluating, for pause and game over. The times of the pieces are frozen, not cleared, and the
        /// Danger Line stays as it was, so a game over leaves it pulsing.
        /// </summary>
        public void Disable()
        {
            _active = false;
        }

        /// <summary>
        /// Starts a new run: clears the time of every piece in play, the progress and the game over flag, and hides
        /// the Danger Line. It does not enable or disable the detector. The owner releases the pieces of the
        /// previous run (<see cref="PieceFactory.ReleaseAll"/>).
        /// </summary>
        public void ResetForNewRun()
        {
            ClearState();
        }

        /// <summary>
        /// Evaluates every piece in play: advances the time of those that are overflowing, resets the others,
        /// updates the Danger Line and the progress, and raises <see cref="GameOverTriggered"/> if a piece reached the
        /// overflow time. Does nothing while disabled, before <see cref="Initialize"/>, or after the game over. Public
        /// so tests can drive it without waiting for physics steps.
        /// </summary>
        /// <param name="deltaTime">Seconds since the previous evaluation, added to the pieces that overflow.</param>
        /// <param name="now">Current time on the clock of <see cref="Piece.SpawnTime"/>, which is <see cref="Time.time"/>.</param>
        public void Evaluate(float deltaTime, float now)
        {
            if (!_active || _triggered || _factory == null)
            {
                return;
            }

            var lineY = _jar.DangerLineY;
            var warnY = lineY - _warnDistance;
            var anyNear = false;
            var anyOverflowing = false;
            var worstSeconds = 0f;

            var pieces = _factory.ActivePieces;
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (piece.IsHeld)
                {
                    piece.ClearOverflowTime();
                    continue;
                }

                // Cheapest first: height, then speed, then age.
                var top = piece.Collider.bounds.max.y;
                anyNear |= top >= warnY;

                if (top > lineY && piece.IsSettled && IsPastGrace(piece, now))
                {
                    piece.AddOverflowTime(deltaTime);
                    anyOverflowing = true;
                    worstSeconds = Mathf.Max(worstSeconds, piece.OverflowSeconds);
                }
                else
                {
                    piece.ClearOverflowTime();
                }
            }

            ShowDanger(anyNear, anyOverflowing);
            SetProgress(_overflowTime > 0f ? Mathf.Clamp01(worstSeconds / _overflowTime) : 1f);

            if (anyOverflowing && worstSeconds >= _overflowTime - TIME_TOLERANCE)
            {
                _triggered = true;
                GameOverTriggered?.Invoke();
            }
        }

        /// <summary>
        /// Accumulates the fixed time and evaluates once per interval, so the cost is 10 passes per second whatever
        /// the physics rate. The evaluation gets the time that really passed, so the timers stay accurate.
        /// </summary>
        private void FixedUpdate()
        {
            if (!_active || _triggered)
            {
                return;
            }

            _accumulator += Time.fixedDeltaTime;
            if (_accumulator < EVALUATION_INTERVAL - TIME_TOLERANCE)
            {
                return;
            }

            var elapsed = _accumulator;
            _accumulator = 0f;
            Evaluate(elapsed, Time.time);
        }

        /// <summary>
        /// Whether the piece is old enough to count: past the overflow grace since it was created, and past the
        /// spawn grace a merge stamps on the piece it creates.
        /// </summary>
        private bool IsPastGrace(Piece piece, float now)
        {
            return now - piece.SpawnTime > _overflowGrace && now >= piece.SpawnGraceUntil;
        }

        /// <summary>
        /// Shows the Danger Line while a piece is near it and pulses it while a piece overflows.
        /// </summary>
        private void ShowDanger(bool visible, bool pulsing)
        {
            var line = _jar.DangerLine;
            if (line == null)
            {
                return;
            }

            if (line.IsVisible != visible)
            {
                line.SetVisible(visible);
            }

            if (line.IsPulsing != pulsing)
            {
                line.SetPulse(pulsing);
            }
        }

        /// <summary>
        /// Stores the worst progress and raises the event when it changed.
        /// </summary>
        private void SetProgress(float progress)
        {
            if (Mathf.Approximately(progress, WorstOverflowProgress))
            {
                return;
            }

            WorstOverflowProgress = progress;
            OverflowProgressChanged?.Invoke(progress);
        }

        /// <summary>
        /// Forgets everything of a run: the accumulator, the game over flag, the time of the pieces in play, the
        /// progress (raising the event if it was not 0) and the Danger Line.
        /// </summary>
        private void ClearState()
        {
            _accumulator = 0f;
            _triggered = false;

            if (_factory != null)
            {
                var pieces = _factory.ActivePieces;
                for (var i = 0; i < pieces.Count; i++)
                {
                    pieces[i].ClearOverflowTime();
                }
            }

            SetProgress(0f);

            if (_jar != null && _jar.DangerLine != null)
            {
                _jar.DangerLine.SetPulse(false);
                _jar.DangerLine.SetVisible(false);
            }
        }
    }
}
