using System;
using System.Collections.Generic;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Merges touching pieces of the same tier (GDD §3.4, §14.3). The collision callbacks of the pieces only enqueue
    /// the pair in a <see cref="MergePairQueue"/>; once per physics step, in <see cref="FixedUpdate"/>, the queue is
    /// processed in a stable order: both pieces are released and one piece of the next tier is created at their
    /// midpoint with their average velocity. Nothing is created or released inside a collision callback (S-62).
    /// <para>
    /// A piece joins at most one merge per step. A piece created by a merge that already touches another one of its
    /// tier merges in the next step, because the contact is reported again, so chains play out visibly. Two touching
    /// pieces of the last tier (the Black Hole) are both released and nothing replaces them: that raises
    /// <see cref="SupernovaTriggered"/>.
    /// </para>
    /// <para>
    /// It scores nothing and plays no effects: <see cref="Merged"/> and <see cref="SupernovaTriggered"/> are for the
    /// score system, the effects and the stats. It receives everything through <see cref="Initialize"/> (S-22).
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/Merge System")]
    [DisallowMultipleComponent]
    public class MergeSystem : MonoBehaviour
    {
        // Slack on the sum of the radii when checking that a queued pair still touches, for contact offsets.
        private const float CONTACT_TOLERANCE = 0.1f;

        private readonly MergePairQueue _queue = new();
        private Action<Piece, Piece> _onCollided;
        private Action<Piece> _onPieceCreated;
        private PieceFactory _factory;
        private IReadOnlyList<TierDefinition> _tiers;
        private GameConfig _config;
        private bool _subscribed;
        private bool _creationErrorLogged;

        /// <summary>
        /// Raised after a merge with the tier index of the new piece, and its position and velocity.
        /// </summary>
        public event Action<int, Vector2, Vector2> Merged;

        /// <summary>
        /// Raised with the position where two Black Holes touched and vanished.
        /// </summary>
        public event Action<Vector2> SupernovaTriggered;

        /// <summary>
        /// Listens to the pieces again when the component is re-enabled after <see cref="Initialize"/> (S-23).
        /// </summary>
        private void OnEnable()
        {
            Subscribe();
        }

        /// <summary>
        /// Stops listening to the pieces and forgets the pairs that were waiting.
        /// </summary>
        private void OnDisable()
        {
            Unsubscribe();
            _queue.Clear();
        }

        /// <summary>
        /// Merges the pairs queued by the contacts of the last physics step.
        /// </summary>
        private void FixedUpdate()
        {
            ProcessQueue();
        }

        /// <summary>
        /// Gives the system what it works with and starts listening to the pieces of the factory. Call it once,
        /// before the first physics step of the run; calling it again replaces the factory.
        /// </summary>
        /// <param name="factory">Creates and releases the pieces. It must be pre-warmed with every tier.</param>
        /// <param name="tiers">Every tier, in tier order, so a merge finds the definition of the next tier.</param>
        /// <param name="config">Source of the overflow grace stamped on the pieces a merge creates.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public void Initialize(PieceFactory factory, IReadOnlyList<TierDefinition> tiers, GameConfig config)
        {
            var newFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            var newTiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            var newConfig = config != null ? config : throw new ArgumentNullException(nameof(config));

            Unsubscribe();
            _queue.Clear();
            _factory = newFactory;
            _tiers = newTiers;
            _config = newConfig;
            _onCollided ??= OnCollided;
            _onPieceCreated ??= HookPiece;
            Subscribe();
        }

        /// <summary>
        /// Starts a new run: forgets the waiting pairs and lets the next creation error be logged again. The owner
        /// releases the pieces of the previous run (<see cref="PieceFactory.ReleaseAll"/>).
        /// </summary>
        public void ResetForNewRun()
        {
            _queue.Clear();
            _creationErrorLogged = false;
        }

        /// <summary>
        /// Merges every queued pair, in order. Public so tests can drive it without waiting for a physics step.
        /// </summary>
        public void ProcessQueue()
        {
            if (_queue.Count == 0)
            {
                return;
            }

            _queue.Sort();
            for (var i = 0; i < _queue.Count; i++)
            {
                var pair = _queue[i];
                if (CanStillMerge(pair))
                {
                    Resolve(pair.Low, pair.High);
                }
            }

            _queue.Clear();
        }

        /// <summary>
        /// Queues the pair of a contact. Runs inside the collision callbacks, so it only enqueues.
        /// </summary>
        private void OnCollided(Piece reporter, Piece other)
        {
            _queue.TryEnqueue(reporter, other);
        }

        /// <summary>
        /// Starts listening to the contacts of a piece. Safe to call twice for the same piece.
        /// </summary>
        private void HookPiece(Piece piece)
        {
            piece.Collided -= _onCollided;
            piece.Collided += _onCollided;
        }

        /// <summary>
        /// Listens to the factory and to the pieces already in play. Does nothing before <see cref="Initialize"/>,
        /// while disabled, or when already listening.
        /// </summary>
        private void Subscribe()
        {
            if (_subscribed || _factory == null || !isActiveAndEnabled)
            {
                return;
            }

            _subscribed = true;
            _factory.PieceCreated += _onPieceCreated;

            var active = _factory.ActivePieces;
            for (var i = 0; i < active.Count; i++)
            {
                HookPiece(active[i]);
            }
        }

        /// <summary>
        /// Stops listening to the factory and to the pieces in play.
        /// </summary>
        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            _factory.PieceCreated -= _onPieceCreated;

            var active = _factory.ActivePieces;
            for (var i = 0; i < active.Count; i++)
            {
                active[i].Collided -= _onCollided;
            }
        }

        /// <summary>
        /// Whether a queued pair is still valid when its turn comes: an earlier pair of this step may have merged one
        /// of the pieces, or the pieces may have been released, held, reused as another tier or moved apart (for
        /// instance by a restart) since the contact was reported.
        /// </summary>
        private static bool CanStillMerge(MergePair pair)
        {
            var low = pair.Low;
            var high = pair.High;
            if (low.Merged || high.Merged || low.IsHeld || high.IsHeld)
            {
                return false;
            }

            if (!low.gameObject.activeInHierarchy || !high.gameObject.activeInHierarchy || low.Tier != high.Tier)
            {
                return false;
            }

            var reach = (low.Collider.radius + high.Collider.radius) * low.transform.lossyScale.x + CONTACT_TOLERANCE;
            return (low.Rigidbody.position - high.Rigidbody.position).sqrMagnitude <= reach * reach;
        }

        /// <summary>
        /// Merges one pair: reads the bodies before anything is released, creates the next tier, and only then
        /// releases the originals, so a failed creation leaves both pieces untouched.
        /// </summary>
        private void Resolve(Piece low, Piece high)
        {
            var midpoint = (low.Rigidbody.position + high.Rigidbody.position) * 0.5f;
            var velocity = (low.Rigidbody.linearVelocity + high.Rigidbody.linearVelocity) * 0.5f;
            var nextTier = low.Tier.Index + 1;

            if (nextTier >= _tiers.Count)
            {
                ReleasePair(low, high);
                SupernovaTriggered?.Invoke(midpoint);
                return;
            }

            if (!TryCreate(_tiers[nextTier], midpoint, velocity, out var created))
            {
                return;
            }

            ReleasePair(low, high);
            created.StampSpawnGrace(Time.time + _config.OverflowGrace);
            Merged?.Invoke(nextTier, midpoint, velocity);
        }

        /// <summary>
        /// Asks the factory for the merged piece. A failure is a setup problem (an unknown tier, a factory that was
        /// not pre-warmed): it is logged once per run, because the pair would fail again every step.
        /// </summary>
        private bool TryCreate(TierDefinition tier, Vector2 position, Vector2 velocity, out Piece created)
        {
            try
            {
                created = _factory.Create(tier, position, velocity);
            }
            catch (InvalidOperationException e)
            {
                created = null;
                LogCreationError(e.Message);
                return false;
            }

            if (created != null)
            {
                return true;
            }

            LogCreationError("The factory returned no piece.");
            return false;
        }

        /// <summary>
        /// Marks both pieces as merged, so no later pair of this step uses them, and gives them back to the factory.
        /// </summary>
        private void ReleasePair(Piece low, Piece high)
        {
            low.MarkMerged();
            high.MarkMerged();
            _factory.Release(low);
            _factory.Release(high);
        }

        /// <summary>
        /// Logs a failed merge the first time it happens in a run.
        /// </summary>
        private void LogCreationError(string reason)
        {
            if (_creationErrorLogged)
            {
                return;
            }

            _creationErrorLogged = true;
            Debug.LogError($"MergeSystem could not create the merged piece, so both pieces were left untouched: {reason}", this);
        }
    }
}
