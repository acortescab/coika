using System;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// One piece of the game: a body, a collider and a sprite configured for a tier. All pieces (held, falling,
    /// merged) are the same prefab, so a piece is reused by calling <see cref="Initialize"/> again. It never merges
    /// anything itself: it only reports its contacts through <see cref="Collided"/>, and the merge system (issue
    /// #7) decides what to do.
    /// <para>
    /// It does not load assets (C-01): the sprite is loaded by the factory through the asset service and passed in.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D))]
    [DisallowMultipleComponent]
    public class Piece : MonoBehaviour
    {
        /// <summary>Name of the physics layer of a piece that is in play.</summary>
        public const string LAYER_NAME = "Piece";

        /// <summary>Name of the physics layer of the piece the player is holding.</summary>
        public const string HELD_LAYER_NAME = "HeldPiece";

        private SpriteRenderer _spriteRenderer;
        private Rigidbody2D _rigidbody;
        private CircleCollider2D _collider;
        private float _settledVelocitySquared;
        private int _pieceLayer = -1;
        private int _heldLayer = -1;

        /// <summary>
        /// Raised with (this piece, the other piece) every physics step two pieces touch: when they start touching
        /// and while they stay in contact, which chain merges need. Pieces that touch anything else raise nothing.
        /// </summary>
        public event Action<Piece, Piece> Collided;

        /// <summary>The tier this piece was initialized with. Null until <see cref="Initialize"/> runs.</summary>
        public TierDefinition Tier { get; private set; }

        /// <summary>Whether the piece has already been merged, so it is not merged twice.</summary>
        public bool Merged { get; private set; }

        /// <summary>Value of <see cref="Time.time"/> when the piece was initialized.</summary>
        public float SpawnTime { get; private set; }

        /// <summary>
        /// Creation order within the run, assigned by the factory. The merge system uses it as the stable key that
        /// decides which piece of a pair owns the merge (lower wins), because Unity instance IDs of pooled pieces
        /// are not the same from one run to the next.
        /// </summary>
        public int SequenceId { get; private set; }

        /// <summary>Value of <see cref="Time.time"/> until which the piece does not count for the overflow timer.</summary>
        public float SpawnGraceUntil { get; private set; }

        /// <summary>Whether the piece was created by a merge a moment ago and is still in its overflow grace.</summary>
        public bool IsInSpawnGrace => IsInSpawnGraceAt(Time.time);

        /// <summary>Whether the piece is still in its overflow grace at the given time.</summary>
        /// <param name="now">Current time on the clock of <see cref="SpawnTime"/>.</param>
        /// <returns>True while <paramref name="now"/> is before <see cref="SpawnGraceUntil"/>.</returns>
        public bool IsInSpawnGraceAt(float now)
        {
            return now < SpawnGraceUntil;
        }

        /// <summary>
        /// Seconds the piece has been continuously overflowing, for the overflow detector (issue #9). It lives on the
        /// piece so tracking costs no lookup or allocation, and a piece that goes back to the pool and is reused
        /// starts at 0 because <see cref="Initialize"/> clears it.
        /// </summary>
        public float OverflowSeconds { get; private set; }

        /// <summary>Whether the player is holding the piece: kinematic, on the held layer and without collisions.</summary>
        public bool IsHeld { get; private set; }

        /// <summary>The body of the piece.</summary>
        public Rigidbody2D Rigidbody
        {
            get
            {
                if (_rigidbody == null)
                    _rigidbody = GetComponent<Rigidbody2D>();

                return _rigidbody;
            }
        }

        /// <summary>The circle collider of the piece.</summary>
        public CircleCollider2D Collider
        {
            get
            {
                if (_collider == null)
                    _collider = GetComponent<CircleCollider2D>();

                return _collider;
            }
        }

        /// <summary>Whether the piece moves slower than the settled velocity of the config.</summary>
        public bool IsSettled => Rigidbody.linearVelocity.sqrMagnitude < _settledVelocitySquared;

        /// <summary>
        /// Configures the piece for a tier and resets everything a previous use could have left: scale, rotation,
        /// velocity, the merged and held flags, the body type, the layer and the collider. The collider radius is
        /// half the tier diameter and the mass is the area of the circle (pi times the radius squared), so bigger
        /// pieces are heavier. Positioning the piece is the caller's job.
        /// </summary>
        /// <param name="tier">The tier to become.</param>
        /// <param name="sprite">Sprite of the tier, already loaded by the caller.</param>
        /// <param name="config">Source of the settled velocity.</param>
        /// <exception cref="ArgumentNullException">The tier, the sprite or the config is null.</exception>
        /// <exception cref="InvalidOperationException">The Piece or HeldPiece physics layer is not defined.</exception>
        public void Initialize(TierDefinition tier, Sprite sprite, GameConfig config)
        {
            Initialize(tier, sprite, config, Time.time);
        }

        /// <summary>
        /// Same as <see cref="Initialize(TierDefinition, Sprite, GameConfig)"/>, stamping <see cref="SpawnTime"/> with
        /// the given time instead of <see cref="Time.time"/>, so a simulation that owns its clock stays consistent.
        /// </summary>
        /// <param name="tier">The tier to become.</param>
        /// <param name="sprite">Sprite of the tier, already loaded by the caller.</param>
        /// <param name="config">Source of the settled velocity.</param>
        /// <param name="spawnTime">Current time on the clock the overflow detector uses.</param>
        /// <exception cref="ArgumentNullException">The tier, the sprite or the config is null.</exception>
        /// <exception cref="InvalidOperationException">The Piece or HeldPiece physics layer is not defined.</exception>
        public void Initialize(TierDefinition tier, Sprite sprite, GameConfig config, float spawnTime)
        {
            if (tier == null)
                throw new ArgumentNullException(nameof(tier));

            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            ResolveLayers();

            Tier = tier;
            Merged = false;
            SpawnTime = spawnTime;
            SpawnGraceUntil = 0f;
            OverflowSeconds = 0f;
            _settledVelocitySquared = config.SettledVelocity * config.SettledVelocity;

            var radius = tier.DiameterUnits * 0.5f;
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            _spriteRenderer.sprite = sprite;
            Collider.radius = radius;

            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;

            var body = Rigidbody;
            body.mass = Mathf.PI * radius * radius;
            body.rotation = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            SetHeld(false);
        }

        /// <summary>
        /// Holds or releases the piece. A held piece is kinematic, on the held layer and has its collider off, so
        /// it follows the pointer without pushing anything. Releasing makes it dynamic again, on the piece layer
        /// and with its collider on. Either way its velocity is cleared.
        /// </summary>
        /// <param name="held">True to hold the piece, false to release it.</param>
        /// <exception cref="InvalidOperationException">The Piece or HeldPiece physics layer is not defined.</exception>
        public void SetHeld(bool held)
        {
            ResolveLayers();

            IsHeld = held;

            var body = Rigidbody;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = held ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;

            gameObject.layer = held ? _heldLayer : _pieceLayer;
            Collider.enabled = !held;
        }

        /// <summary>
        /// Whether this piece and another one are allowed to merge right now (GDD §3.4): they are two different
        /// pieces of the same tier, and neither is held nor already merged. It does not look at where the pieces
        /// are or who owns the pair; the merge queue and the merge system add those rules on top of this one, so the
        /// eligibility rules live in a single place.
        /// </summary>
        /// <param name="other">The piece to merge with. May be null.</param>
        /// <returns>True when both pieces can take part in a merge.</returns>
        public bool CanMergeWith(Piece other)
        {
            if (other == null || other == this)
            {
                return false;
            }

            if (Tier == null || Tier != other.Tier)
            {
                return false;
            }

            return !Merged && !other.Merged && !IsHeld && !other.IsHeld;
        }

        /// <summary>
        /// Marks the piece as merged. The merge system calls it before releasing the piece, so a piece in a chain
        /// is never merged twice.
        /// </summary>
        public void MarkMerged()
        {
            Merged = true;
        }

        /// <summary>
        /// Gives the piece its creation order within the run. The factory calls it right after
        /// <see cref="Initialize"/>.
        /// </summary>
        /// <param name="sequenceId">Order of creation, from 0 at the start of a run.</param>
        public void AssignSequenceId(int sequenceId)
        {
            SequenceId = sequenceId;
        }

        /// <summary>
        /// Stamps the time until which the piece is exempt from the overflow timer. The merge system calls it on the
        /// piece it creates; this class only stores it, the overflow detector (issue #9) reads it.
        /// </summary>
        /// <param name="graceUntil">Value of <see cref="Time.time"/> at which the grace ends.</param>
        public void StampSpawnGrace(float graceUntil)
        {
            SpawnGraceUntil = graceUntil;
        }

        /// <summary>
        /// Adds time to the continuous overflow of the piece. Only the overflow detector calls it.
        /// </summary>
        /// <param name="seconds">Seconds to add.</param>
        public void AddOverflowTime(float seconds)
        {
            OverflowSeconds += seconds;
        }

        /// <summary>
        /// Sets the continuous overflow back to 0, because the piece stopped overflowing.
        /// </summary>
        public void ClearOverflowTime()
        {
            OverflowSeconds = 0f;
        }

        /// <summary>
        /// Removes every subscriber of <see cref="Collided"/>. The factory calls it when it takes the piece back,
        /// so a reused piece never keeps listeners of its previous life.
        /// </summary>
        public void ClearCollidedSubscribers()
        {
            Collided = null;
        }

        /// <summary>
        /// Reports the start of a contact with another piece.
        /// </summary>
        /// <param name="collision">The contact.</param>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            RaiseCollided(collision);
        }

        /// <summary>
        /// Reports a contact that continues with another piece, every physics step.
        /// </summary>
        /// <param name="collision">The contact.</param>
        private void OnCollisionStay2D(Collision2D collision)
        {
            RaiseCollided(collision);
        }

        /// <summary>
        /// Raises <see cref="Collided"/> when the other collider belongs to a piece. Allocation free: it runs
        /// every physics step for every resting contact.
        /// </summary>
        /// <param name="collision">The contact.</param>
        private void RaiseCollided(Collision2D collision)
        {
            if (Collided == null)
                return;

            if (collision.collider.TryGetComponent<Piece>(out var other))
                Collided.Invoke(this, other);
        }

        /// <summary>
        /// Looks up the indices of the piece and held layers once. A missing layer is a project configuration
        /// error that leaves the piece unusable, so it throws instead of letting the caller carry on with a
        /// half-configured piece.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Piece or HeldPiece physics layer is not defined.</exception>
        private void ResolveLayers()
        {
            if (_pieceLayer >= 0 && _heldLayer >= 0)
                return;

            _pieceLayer = LayerMask.NameToLayer(LAYER_NAME);
            _heldLayer = LayerMask.NameToLayer(HELD_LAYER_NAME);
            if (_pieceLayer < 0 || _heldLayer < 0)
                throw new InvalidOperationException($"The physics layers '{LAYER_NAME}' and '{HELD_LAYER_NAME}' must be defined.");
        }
    }
}
