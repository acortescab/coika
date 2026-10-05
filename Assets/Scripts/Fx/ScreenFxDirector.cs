using System;
using Coika.Data;
using Coika.Gameplay;
using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// Decides when the screen effects happen: it listens to the merge system and asks for a shake and a slow-mo on
    /// the merges of the heavy tiers, and for a shake and a white flash on a supernova. It is purely visual: it
    /// changes neither physics nor score. With <see cref="ReduceShake"/> on there is no shake and no slow-mo; the
    /// flash stays, because its rate is limited by the overlay itself. The handlers are cached delegates, so
    /// listening and reacting allocate nothing.
    /// </summary>
    public class ScreenFxDirector
    {
        private readonly IScreenShake _shake;
        private readonly ISlowMo _slowMo;
        private readonly IScreenFlash _flash;
        private readonly FeedbackConfig _config;
        private readonly Action<int, Vector2, Vector2> _onMerged;
        private readonly Action<Vector2> _onSupernova;

        private MergeSystem _merge;
        private bool _reduceShake;

        /// <summary>
        /// Creates a director. Call <see cref="Bind"/> to start listening.
        /// </summary>
        /// <param name="shake">Moves the camera rig.</param>
        /// <param name="slowMo">Slows time down.</param>
        /// <param name="flash">Flashes the screen.</param>
        /// <param name="config">Thresholds, amplitudes and durations.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public ScreenFxDirector(IScreenShake shake, ISlowMo slowMo, IScreenFlash flash, FeedbackConfig config)
        {
            _shake = shake ?? throw new ArgumentNullException(nameof(shake));
            _slowMo = slowMo ?? throw new ArgumentNullException(nameof(slowMo));
            _flash = flash ?? throw new ArgumentNullException(nameof(flash));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _onMerged = OnMerged;
            _onSupernova = OnSupernova;
        }

        /// <summary>
        /// Gets or sets the Reduce Shake accessibility setting. Turning it on stops the shake and the slow-mo that
        /// are running.
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
        /// Starts listening. Binding again first stops listening to the previous system.
        /// </summary>
        /// <param name="merge">Source of the merge and supernova events.</param>
        /// <exception cref="ArgumentNullException">The merge system is null.</exception>
        public void Bind(MergeSystem merge)
        {
            var newMerge = merge != null ? merge : throw new ArgumentNullException(nameof(merge));

            Unbind();
            _merge = newMerge;
            _merge.Merged += _onMerged;
            _merge.SupernovaTriggered += _onSupernova;
        }

        /// <summary>
        /// Stops listening. Safe to call when not bound. A destroyed merge system is released without unsubscribing.
        /// </summary>
        public void Unbind()
        {
            if (_merge != null)
            {
                _merge.Merged -= _onMerged;
                _merge.SupernovaTriggered -= _onSupernova;
            }

            _merge = null;
        }

        /// <summary>
        /// Asks for the shake and the slow-mo of a heavy merge.
        /// </summary>
        /// <param name="tier">Index of the tier of the piece the merge created.</param>
        /// <param name="position">Where it appeared, unused.</param>
        /// <param name="velocity">Its velocity, unused.</param>
        private void OnMerged(int tier, Vector2 position, Vector2 velocity)
        {
            if (_reduceShake || tier < _config.HeavyMergeMinTier)
            {
                return;
            }

            _shake.Shake(_config.MergeShakeAmplitude(tier), _config.ShakeDuration);
            _slowMo.Begin(_config.SlowMoScale, _config.SlowMoDuration);
        }

        /// <summary>
        /// Asks for the shake and the white flash of a supernova.
        /// </summary>
        /// <param name="midpoint">Middle of the two Black Holes, unused.</param>
        private void OnSupernova(Vector2 midpoint)
        {
            if (!_reduceShake)
            {
                _shake.Shake(_config.SupernovaShakeAmplitude, _config.SupernovaShakeDuration);
            }

            _flash.Flash(_config.ScreenFlashDuration);
        }
    }
}
