using System;
using System.Collections.Generic;
using Coika.Core;
using Coika.Data;
using Coika.Gameplay;
using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// The single place that maps each gameplay event to its feedback bundle of GDD §9: particles, screen effects,
    /// sound and haptics. Gameplay code raises events and knows nothing of this class, so leaving it unbound changes
    /// neither physics, score nor the simulation result. The numbers live in <see cref="FeedbackConfig"/> and the
    /// formulas in its pure helpers and in <see cref="AudioMath"/>; this class only switches between them.
    /// <para>
    /// Nothing fires unless a run is being played, so a paused game is silent; the only event that plays outside a run is the game over itself. The sound and the haptic of merges
    /// are held until <see cref="Tick"/>, which plays one of each for all the merges of a chain, with the highest
    /// tier. The handlers are cached delegates, so listening and reacting allocate nothing.
    /// </para>
    /// </summary>
    public class FeedbackDirector
    {
        private const float DUST_WHITE_BLEND = 0.6f;

        private readonly IAudioService _audio;
        private readonly IHaptics _haptics;
        private readonly IParticleSpawner _spawner;
        private readonly IScreenShake _shake;
        private readonly ISlowMo _slowMo;
        private readonly IScreenFlash _flash;
        private readonly FeedbackConfig _config;
        private readonly IReadOnlyList<TierDefinition> _tiers;
        private readonly Func<double> _clock;

        private readonly Action<int, Vector2, Vector2> _onMerged;
        private readonly Action<Vector2> _onSupernova;
        private readonly Action _onNewBest;
        private readonly Action<Piece> _onPieceCreated;
        private readonly Action<Piece, float> _onLanded;
        private readonly Action<int> _onPieceDropped;
        private readonly Action<int> _onPieceSpawned;
        private readonly Action<bool> _onDangerChanged;
        private readonly Action<GameState, GameState> _onStateChanged;
        private readonly Action<RunContext> _onRunStarted;
        private readonly Action<RunSummary> _onRunEnded;
        private readonly Action<RunSummary> _onGameOverReady;

        private MergeSystem _merge;
        private ScoreSystem _score;
        private DropController _drop;
        private OverflowDetector _overflow;
        private PieceFactory _factory;
        private GameManager _manager;
        private Jar _jar;

        private bool _reduceShake;
        private bool _playing;
        private bool _danger;
        private double _nextTick;
        private bool _hasPendingMerge;
        private int _pendingTier;
        private int _pendingComboStep;

        /// <summary>
        /// Creates a director. Call <see cref="Bind"/> to start listening.
        /// </summary>
        /// <param name="audio">Plays the sounds. Null when the game has no audio: sounds are then skipped.</param>
        /// <param name="haptics">Plays the vibrations. Null when the device has none: they are then skipped.</param>
        /// <param name="spawner">Draws the particles and rings.</param>
        /// <param name="shake">Moves the camera rig.</param>
        /// <param name="slowMo">Slows time down.</param>
        /// <param name="flash">Flashes the screen.</param>
        /// <param name="config">The mapping table: thresholds, counts, pitches and durations.</param>
        /// <param name="tiers">Every tier, in tier order, for the colours and sizes.</param>
        /// <param name="clock">Seconds on a clock that keeps running while the game is paused, for the danger tick.</param>
        /// <exception cref="ArgumentNullException">A dependency other than the audio and the haptics is null.</exception>
        public FeedbackDirector(
            IAudioService audio,
            IHaptics haptics,
            IParticleSpawner spawner,
            IScreenShake shake,
            ISlowMo slowMo,
            IScreenFlash flash,
            FeedbackConfig config,
            IReadOnlyList<TierDefinition> tiers,
            Func<double> clock)
        {
            _audio = audio;
            _haptics = haptics;
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _shake = shake ?? throw new ArgumentNullException(nameof(shake));
            _slowMo = slowMo ?? throw new ArgumentNullException(nameof(slowMo));
            _flash = flash ?? throw new ArgumentNullException(nameof(flash));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            _onMerged = OnMerged;
            _onSupernova = OnSupernova;
            _onNewBest = OnNewBest;
            _onPieceCreated = HookPiece;
            _onLanded = OnLanded;
            _onPieceDropped = OnPieceDropped;
            _onPieceSpawned = OnPieceSpawned;
            _onDangerChanged = OnDangerChanged;
            _onStateChanged = OnStateChanged;
            _onRunStarted = OnRunStarted;
            _onRunEnded = OnRunEnded;
            _onGameOverReady = OnGameOverReady;
        }

        /// <summary>
        /// Gets or sets the Reduce Shake accessibility setting. Turning it on stops the shake and the slow-mo that
        /// are running; while it is on the danger tick is slower, like the pulse of the Danger Line.
        /// </summary>
        public bool ReduceShake
        {
            get => _reduceShake;
            set
            {
                _reduceShake = value;
                if (value)
                {
                    _shake.Clear();
                    _slowMo.Cancel();
                }
            }
        }

        /// <summary>
        /// Starts listening. Binding again first stops listening to the previous systems. Bind after the score system
        /// is built, so the combo is up to date when a merge is played.
        /// </summary>
        /// <param name="merge">Source of the merge and supernova events.</param>
        /// <param name="score">Source of the new best event and the combo.</param>
        /// <param name="drop">Source of the drop and spawn events.</param>
        /// <param name="overflow">Source of the danger state.</param>
        /// <param name="factory">Source of the pieces, whose landings it listens to.</param>
        /// <param name="manager">Source of the run and state events.</param>
        /// <param name="jar">The jar, for the confetti origin and the height of the game-over flash.</param>
        /// <exception cref="ArgumentNullException">A system is null.</exception>
        public void Bind(
            MergeSystem merge,
            ScoreSystem score,
            DropController drop,
            OverflowDetector overflow,
            PieceFactory factory,
            GameManager manager,
            Jar jar)
        {
            var newMerge = merge != null ? merge : throw new ArgumentNullException(nameof(merge));
            var newScore = score ?? throw new ArgumentNullException(nameof(score));
            var newDrop = drop != null ? drop : throw new ArgumentNullException(nameof(drop));
            var newOverflow = overflow != null ? overflow : throw new ArgumentNullException(nameof(overflow));
            var newFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            var newManager = manager ?? throw new ArgumentNullException(nameof(manager));
            var newJar = jar != null ? jar : throw new ArgumentNullException(nameof(jar));

            Unbind();
            _merge = newMerge;
            _score = newScore;
            _drop = newDrop;
            _overflow = newOverflow;
            _factory = newFactory;
            _manager = newManager;
            _jar = newJar;
            _playing = _manager.State == GameState.Playing;

            _merge.Merged += _onMerged;
            _merge.SupernovaTriggered += _onSupernova;
            _score.NewBestReached += _onNewBest;
            _drop.PieceDropped += _onPieceDropped;
            _drop.PieceSpawned += _onPieceSpawned;
            _overflow.DangerChanged += _onDangerChanged;
            _factory.PieceCreated += _onPieceCreated;
            _manager.StateChanged += _onStateChanged;
            _manager.RunStarted += _onRunStarted;
            _manager.RunEnded += _onRunEnded;
            _manager.GameOverReady += _onGameOverReady;

            var active = _factory.ActivePieces;
            for (var i = 0; i < active.Count; i++)
            {
                HookPiece(active[i]);
            }
        }

        /// <summary>
        /// Stops listening to every system. Safe to call when not bound.
        /// </summary>
        public void Unbind()
        {
            // Checks the plain-class score, not the MonoBehaviours: a destroyed one compares equal to null and would
            // leave the other handlers attached.
            if (_score == null)
            {
                return;
            }

            _merge.Merged -= _onMerged;
            _merge.SupernovaTriggered -= _onSupernova;
            _score.NewBestReached -= _onNewBest;
            _drop.PieceDropped -= _onPieceDropped;
            _drop.PieceSpawned -= _onPieceSpawned;
            _overflow.DangerChanged -= _onDangerChanged;
            _factory.PieceCreated -= _onPieceCreated;
            _manager.StateChanged -= _onStateChanged;
            _manager.RunStarted -= _onRunStarted;
            _manager.RunEnded -= _onRunEnded;
            _manager.GameOverReady -= _onGameOverReady;

            var active = _factory.ActivePieces;
            for (var i = 0; i < active.Count; i++)
            {
                active[i].Landed -= _onLanded;
            }

            _merge = null;
            _score = null;
            _drop = null;
            _overflow = null;
            _factory = null;
            _manager = null;
            _jar = null;
            _playing = false;
            _danger = false;
            _hasPendingMerge = false;
        }

        /// <summary>
        /// Stops every shake, slow-mo and flash that is running, for instance when the game is paused or a new run
        /// starts.
        /// </summary>
        public void StopAll()
        {
            _shake.Clear();
            _slowMo.Cancel();
            _flash.Clear();
        }

        /// <summary>
        /// Plays what is held for the frame: one merge sound and haptic for all the merges since the last call, and
        /// the danger tick when it is due. Call it once per frame, after the physics step.
        /// </summary>
        public void Tick()
        {
            if (!_playing)
            {
                return;
            }

            if (_hasPendingMerge)
            {
                PlayMerge(_pendingTier, _pendingComboStep);
                _hasPendingMerge = false;
            }

            if (_danger)
            {
                var now = _clock();
                if (now >= _nextTick)
                {
                    _audio?.PlaySfx(SfxId.DangerTick);
                    _nextTick = now + 1.0 / (_reduceShake ? _config.Sound.DangerTickSoftHz : _config.Sound.DangerTickHz);
                }
            }
        }

        /// <summary>
        /// Emits the burst in the colour of the created tier and the white flash ring, shakes the screen on the
        /// heavy tiers, and holds the sound and the haptic for <see cref="Tick"/>.
        /// </summary>
        /// <param name="tier">Index of the tier of the piece the merge created.</param>
        /// <param name="position">Where it appeared.</param>
        /// <param name="velocity">Its velocity, unused.</param>
        private void OnMerged(int tier, Vector2 position, Vector2 velocity)
        {
            if (!_playing)
            {
                return;
            }

            var color = tier >= 0 && tier < _tiers.Count ? _tiers[tier].TierColor : Color.white;
            _spawner.Burst(FxKind.MergeBurst, position, color, _config.Particles.MergeBurstCount(tier, _tiers.Count));
            _spawner.Burst(FxKind.FlashRing, position, Color.white, 1);

            if (!_reduceShake && tier >= _config.ScreenFx.HeavyMergeMinTier)
            {
                _shake.Shake(_config.ScreenFx.MergeShakeAmplitude(tier), _config.ScreenFx.ShakeDuration);
                _slowMo.Begin(_config.ScreenFx.SlowMoScale, _config.ScreenFx.SlowMoDuration);
            }

            // The combo is read now, not in Tick, because it may expire in between. The later of two equal tiers wins.
            if (!_hasPendingMerge || tier >= _pendingTier)
            {
                _pendingTier = tier;
                _pendingComboStep = Math.Max(_score.ComboTracker.Combo - 1, 0);
            }

            _hasPendingMerge = true;
        }

        /// <summary>
        /// Plays the sound and the haptic of a merge: the pitched pop, layered with the deep sound on the heavy tiers.
        /// </summary>
        /// <param name="tier">The highest tier merged since the last <see cref="Tick"/>.</param>
        /// <param name="comboStep">The combo step of that merge.</param>
        private void PlayMerge(int tier, int comboStep)
        {
            _audio?.PlaySfx(SfxId.Merge, AudioMath.MergePitch(tier, comboStep));
            if (tier >= _config.ScreenFx.HeavyMergeMinTier)
            {
                _audio?.PlaySfx(SfxId.MergeBig);
            }

            _haptics?.Play(_config.Sound.MergeHaptic(tier));
        }

        /// <summary>
        /// Emits the white flash, the shockwave ring, the shake, the screen flash, the boom and the haptic of a
        /// supernova.
        /// </summary>
        /// <param name="midpoint">Middle of the two Black Holes.</param>
        private void OnSupernova(Vector2 midpoint)
        {
            if (!_playing)
            {
                return;
            }

            _spawner.Burst(FxKind.SupernovaFlash, midpoint, Color.white, 1);
            _spawner.Burst(FxKind.Shockwave, midpoint, Color.white, 1);
            if (!_reduceShake)
            {
                _shake.Shake(_config.ScreenFx.SupernovaShakeAmplitude, _config.ScreenFx.SupernovaShakeDuration);
            }

            _flash.Flash(_config.ScreenFx.ScreenFlashDuration);
            _audio?.PlaySfx(SfxId.Supernova);
            _haptics?.Play(HapticKind.Heavy);
        }

        /// <summary>
        /// Emits the confetti and the fanfare of a new best score.
        /// </summary>
        private void OnNewBest()
        {
            if (!_playing)
            {
                return;
            }

            _spawner.Burst(FxKind.Confetti, new Vector2(_jar.transform.position.x, _jar.DangerLineY), Color.white, _config.Particles.ConfettiCount);
            _audio?.PlaySfx(SfxId.NewBest);
        }

        /// <summary>
        /// Plays the whoosh and the light tap of a drop. The stretch of the piece is played by the piece itself.
        /// </summary>
        /// <param name="tier">Tier of the dropped piece, unused.</param>
        private void OnPieceDropped(int tier)
        {
            if (!_playing)
            {
                return;
            }

            _audio?.PlaySfx(SfxId.Drop, _config.Sound.DropPitch);
            _haptics?.Play(HapticKind.Light);
        }

        /// <summary>
        /// Plays the bloop of the next piece appearing. The pop of the piece is played by the piece itself.
        /// </summary>
        /// <param name="tier">Tier of the new piece, unused.</param>
        private void OnPieceSpawned(int tier)
        {
            if (_playing)
            {
                _audio?.PlaySfx(SfxId.Spawn);
            }
        }

        /// <summary>
        /// Starts or stops the danger tick. It starts on the next <see cref="Tick"/>.
        /// </summary>
        /// <param name="danger">Whether a piece is overflowing.</param>
        private void OnDangerChanged(bool danger)
        {
            _danger = danger;
            _nextTick = 0.0;
        }

        /// <summary>
        /// Starts listening to the landings of a piece. Safe to call twice for the same piece.
        /// </summary>
        /// <param name="piece">A piece the factory created.</param>
        private void HookPiece(Piece piece)
        {
            piece.Landed -= _onLanded;
            piece.Landed += _onLanded;
        }

        /// <summary>
        /// When the landing is hard enough, emits a dust puff under the piece and plays the thud, louder with the
        /// impulse and higher for smaller pieces, with a very light haptic.
        /// </summary>
        /// <param name="piece">The piece that landed.</param>
        /// <param name="impulse">Total normal impulse of the landing.</param>
        private void OnLanded(Piece piece, float impulse)
        {
            if (!_playing || impulse < _config.Animations.LandImpulseThreshold || piece.Tier == null)
            {
                return;
            }

            var position = (Vector2)piece.transform.position + Vector2.down * piece.Tier.Radius;
            var color = Color.Lerp(piece.Tier.TierColor, Color.white, DUST_WHITE_BLEND);
            _spawner.Burst(FxKind.LandingDust, position, color, _config.Particles.LandDustCount);
            _audio?.PlaySfx(SfxId.Land, _config.Sound.LandPitch(piece.Tier.Radius), _config.Sound.LandVolume(impulse));
            _haptics?.Play(HapticKind.VeryLight);
        }

        /// <summary>
        /// Goes silent while the game is not being played, and drops what is held and what is running.
        /// </summary>
        /// <param name="previous">The state that was left.</param>
        /// <param name="next">The state that was entered.</param>
        private void OnStateChanged(GameState previous, GameState next)
        {
            _playing = next == GameState.Playing;
            _hasPendingMerge = false;
            _nextTick = 0.0;
            if (next == GameState.Paused)
            {
                // The shake and the flash run on unscaled time, so they would go on moving a paused game.
                StopAll();
            }
        }

        /// <summary>
        /// Forgets the danger and what was held and stops the effects of the previous run.
        /// </summary>
        /// <param name="run">The new run, unused.</param>
        private void OnRunStarted(RunContext run)
        {
            _danger = false;
            _hasPendingMerge = false;
            StopAll();
        }

        /// <summary>
        /// Plays the game-over sound and the long haptic, and starts the flash that sweeps over the pieces from top to bottom.
        /// </summary>
        /// <param name="summary">The summary of the run, unused.</param>
        private void OnRunEnded(RunSummary summary)
        {
            _danger = false;
            _audio?.PlaySfx(SfxId.GameOver);
            _haptics?.Play(HapticKind.Long);

            FlashPieces();
        }

        /// <summary>
        /// Emits the confetti when the Game Over view appears for a run that beat the best score. Confetti is a
        /// celebration, not shake, so reduced motion only lowers its count (in the spawner).
        /// </summary>
        /// <param name="summary">The summary of the run.</param>
        private void OnGameOverReady(RunSummary summary)
        {
            if (summary.IsNewBest)
            {
                _spawner.Burst(FxKind.Confetti, new Vector2(_jar.transform.position.x, _jar.DangerLineY), Color.white, _config.Particles.ConfettiCount);
            }
        }

        /// <summary>
        /// Starts the game-over flash of every piece in play, delayed by its height so it sweeps from the top of the
        /// jar to the bottom. The pieces animate themselves.
        /// </summary>
        private void FlashPieces()
        {
            var top = _jar.DangerLineY;
            var floor = _jar.FloorY;
            var active = _factory.ActivePieces;
            for (var i = 0; i < active.Count; i++)
            {
                var animator = active[i].Animator;
                if (animator != null)
                {
                    animator.Play(PieceEffectId.GameOverFlash, _config.Animations.GameOverDelay(active[i].transform.position.y, top, floor));
                }
            }
        }
    }
}
