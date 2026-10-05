using System;
using System.Collections.Generic;
using Coika.Data;
using Coika.Gameplay;
using UnityEngine;

namespace Coika.Fx
{
    /// <summary>
    /// Decides when each effect happens: it listens to the merge system, the pieces and the score, and asks the
    /// <see cref="IParticleSpawner"/> for the effect, with the tier colour and the counts of <see cref="FeedbackConfig"/>.
    /// It is purely visual and changes neither physics nor score. The handlers are cached delegates, so listening
    /// and reacting allocate nothing.
    /// </summary>
    public class FxDirector
    {
        private const float DUST_WHITE_BLEND = 0.6f;

        private readonly IParticleSpawner _spawner;
        private readonly FeedbackConfig _config;
        private readonly IReadOnlyList<TierDefinition> _tiers;
        private readonly Action<int, Vector2, Vector2> _onMerged;
        private readonly Action<Vector2> _onSupernova;
        private readonly Action _onNewBest;
        private readonly Action<Piece> _onPieceCreated;
        private readonly Action<Piece, float> _onLanded;

        private MergeSystem _merge;
        private ScoreSystem _score;
        private PieceFactory _factory;
        private Vector2 _confettiOrigin;

        /// <summary>
        /// Creates a director. Call <see cref="Bind"/> to start listening.
        /// </summary>
        /// <param name="spawner">Draws the effects.</param>
        /// <param name="config">Counts and landing threshold.</param>
        /// <param name="tiers">Every tier, in tier order, for the colours.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public FxDirector(IParticleSpawner spawner, FeedbackConfig config, IReadOnlyList<TierDefinition> tiers)
        {
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            _onMerged = OnMerged;
            _onSupernova = OnSupernova;
            _onNewBest = OnNewBest;
            _onPieceCreated = HookPiece;
            _onLanded = OnLanded;
        }

        /// <summary>
        /// Starts listening. Binding again first stops listening to the previous systems.
        /// </summary>
        /// <param name="merge">Source of the merge and supernova events.</param>
        /// <param name="score">Source of the new best event.</param>
        /// <param name="factory">Source of the pieces, whose landings it listens to.</param>
        /// <param name="confettiOrigin">World position where the new best confetti starts.</param>
        /// <exception cref="ArgumentNullException">A system is null.</exception>
        public void Bind(MergeSystem merge, ScoreSystem score, PieceFactory factory, Vector2 confettiOrigin)
        {
            var newMerge = merge != null ? merge : throw new ArgumentNullException(nameof(merge));
            var newScore = score ?? throw new ArgumentNullException(nameof(score));
            var newFactory = factory ?? throw new ArgumentNullException(nameof(factory));

            Unbind();
            _merge = newMerge;
            _score = newScore;
            _factory = newFactory;
            _confettiOrigin = confettiOrigin;

            _merge.Merged += _onMerged;
            _merge.SupernovaTriggered += _onSupernova;
            _score.NewBestReached += _onNewBest;
            _factory.PieceCreated += _onPieceCreated;

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
            if (_merge == null)
            {
                return;
            }

            _merge.Merged -= _onMerged;
            _merge.SupernovaTriggered -= _onSupernova;
            _score.NewBestReached -= _onNewBest;
            _factory.PieceCreated -= _onPieceCreated;

            var active = _factory.ActivePieces;
            for (var i = 0; i < active.Count; i++)
            {
                active[i].Landed -= _onLanded;
            }

            _merge = null;
            _score = null;
            _factory = null;
        }

        /// <summary>
        /// Emits the burst in the colour of the created tier and the white flash ring.
        /// </summary>
        /// <param name="tier">Index of the tier of the piece the merge created.</param>
        /// <param name="position">Where it appeared.</param>
        /// <param name="velocity">Its velocity, unused.</param>
        private void OnMerged(int tier, Vector2 position, Vector2 velocity)
        {
            var color = tier >= 0 && tier < _tiers.Count ? _tiers[tier].TierColor : Color.white;
            _spawner.Burst(FxKind.MergeBurst, position, color, _config.MergeBurstCount(tier, _tiers.Count));
            _spawner.Burst(FxKind.FlashRing, position, Color.white, 1);
        }

        /// <summary>
        /// Emits the white flash and the shockwave ring of a supernova.
        /// </summary>
        /// <param name="midpoint">Middle of the two Black Holes.</param>
        private void OnSupernova(Vector2 midpoint)
        {
            _spawner.Burst(FxKind.SupernovaFlash, midpoint, Color.white, 1);
            _spawner.Burst(FxKind.Shockwave, midpoint, Color.white, 1);
        }

        /// <summary>
        /// Emits the confetti of a new best score.
        /// </summary>
        private void OnNewBest()
        {
            _spawner.Burst(FxKind.Confetti, _confettiOrigin, Color.white, _config.ConfettiCount);
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
        /// Emits a dust puff under the piece when the landing is hard enough.
        /// </summary>
        /// <param name="piece">The piece that landed.</param>
        /// <param name="impulse">Total normal impulse of the landing.</param>
        private void OnLanded(Piece piece, float impulse)
        {
            if (impulse < _config.LandImpulseThreshold || piece.Tier == null)
            {
                return;
            }

            var position = (Vector2)piece.transform.position + Vector2.down * piece.Tier.Radius;
            var color = Color.Lerp(piece.Tier.TierColor, Color.white, DUST_WHITE_BLEND);
            _spawner.Burst(FxKind.LandingDust, position, color, _config.LandDustCount);
        }
    }
}
