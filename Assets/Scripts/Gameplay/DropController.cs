using System;
using System.Collections.Generic;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// The only thing the player does: slide the held piece sideways and release it (GDD §3.5, §6, §7). It takes
    /// the current tier from the <see cref="SpawnQueue"/>, asks the <see cref="PieceFactory"/> for a piece, holds it
    /// at the Drop Line, follows the input (keeping the offset the input reports when a press begins) without going
    /// faster than the max follow speed or leaving the jar, and on release makes it fall, advances the queue and
    /// waits for the cooldown before holding the next piece, which grows in meanwhile. A cancelled press goes back
    /// to hovering and drops nothing.
    /// <para>
    /// The rules live in <see cref="DropFlow"/> (plain C#); this component reads the input, applies the result to
    /// the piece and raises the events. It scores nothing: <see cref="PieceDropped"/> is for the score system.
    /// </para>
    /// <para>
    /// It receives everything through <see cref="Initialize"/> (C-01, S-22) and does nothing until
    /// <see cref="Enable"/>. The game manager calls <see cref="Disable"/> on pause and game over and
    /// <see cref="ResetForNewRun"/> on a restart.
    /// </para>
    /// <para>
    /// It copes with the factory taking the held piece back (<see cref="PieceFactory.ReleaseAll"/>): the next frame
    /// it notices and holds a fresh piece of the queue's current tier.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/Drop Controller")]
    [DisallowMultipleComponent]
    public class DropController : MonoBehaviour
    {
        private const float REACH_EPSILON = 0.001f;

#if UNITY_EDITOR
        private const float GIZMO_Z = -1f;
        private static readonly Color RangeGizmoColor = new(0.3f, 0.8f, 1f, 1f); // Light blue
#endif

        private IDropInput _input;
        private Jar _jar;
        private PieceFactory _factory;
        private SpawnQueue _queue;
        private IReadOnlyList<TierDefinition> _tiers;
        private GameConfig _config;
        private DropFlow _flow;
        private Piece _held;
        private float _heldRadius;
        private float _heldX;
        private bool _pressQueued;
        private bool _releaseQueued;
        private bool _cancelQueued;
        private float _followOffset;
        private bool _dropPending;
        private bool _pressFollowsPointer;
        private bool _inputDown;
        private bool _releaseHadPress;
        private float _releaseTargetX;

        /// <summary>Raised when the substate changes: after a release (Dropping) and when the cooldown ends (Aiming).</summary>
        public event Action<DropState> StateChanged;

        /// <summary>Raised after a piece is released, with its tier index, for the drop points of the score system.</summary>
        public event Action<int> PieceDropped;

        /// <summary>The current substate. It is <see cref="DropState.Aiming"/> before the first run.</summary>
        public DropState State => _flow == null ? DropState.Aiming : _flow.State;

        /// <summary>Whether the controller is reading input and holding a piece.</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// Whether the player is pressing on a piece they can control: a finger is down or the drop key is held while
        /// aiming. A press that began during the cooldown counts once the cooldown ends, and letting go drops the
        /// piece (the release decides). The flow's armed press keeps it true while a quickly released piece still
        /// slides to its release point. It ends on the drop, on a cancel, and when the controller is
        /// disabled.
        /// </summary>
        public bool IsPressing => IsEnabled && _flow != null && _flow.State == DropState.Aiming && (_inputDown || _flow.IsPressing);

        /// <summary>The piece being held, or null when the controller is disabled.</summary>
        public Piece HeldPiece => _held;

        /// <summary>World X the held piece is heading to or resting at.</summary>
        public float HeldX => _heldX;

        /// <summary>
        /// Listens to the input again when the component is re-enabled while the controller is enabled (S-23).
        /// </summary>
        private void OnEnable()
        {
            if (IsEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>
        /// Stops listening to the input and forgets the presses not yet acted on, so a deactivated component never
        /// drops a piece when it comes back (S-23).
        /// </summary>
        private void OnDisable()
        {
            Unsubscribe();
            ClearPress();
        }

        /// <summary>
        /// Updates the cooldown and the scale-in and acts on the presses of this frame.
        /// </summary>
        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Moves the held piece with the physics step.
        /// </summary>
        private void FixedUpdate()
        {
            FixedTick(Time.fixedDeltaTime);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draws the X range the held piece can reach, at the Drop Line, so clamping can be checked in the Scene
        /// view. Draws nothing until a piece is held. Editor only.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (_held == null || _jar == null)
            {
                return;
            }

            var left = new Vector3(_jar.InteriorMin.x + _heldRadius, _jar.DropLineY, GIZMO_Z);
            var right = new Vector3(_jar.InteriorMax.x - _heldRadius, _jar.DropLineY, GIZMO_Z);
            Gizmos.color = RangeGizmoColor;
            Gizmos.DrawLine(left, right);
            Gizmos.DrawWireSphere(new Vector3(_heldX, _jar.DropLineY, GIZMO_Z), _heldRadius);
        }
#endif

        /// <summary>
        /// Gives the controller what it works with. Call it once, before <see cref="Enable"/>.
        /// </summary>
        /// <param name="input">Source of the pointer position and its offset, the keyboard axis and the press events.</param>
        /// <param name="jar">The jar: its interior bounds and the Drop Line.</param>
        /// <param name="factory">Creates the pieces. It must be pre-warmed with every tier.</param>
        /// <param name="queue">Decides the tier of each piece.</param>
        /// <param name="tiers">Every tier, in tier order, so the queue's tier index finds its definition.</param>
        /// <param name="config">Source of the cooldown and the follow speed.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public void Initialize(IDropInput input, Jar jar, PieceFactory factory, SpawnQueue queue, IReadOnlyList<TierDefinition> tiers, GameConfig config)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _jar = jar != null ? jar : throw new ArgumentNullException(nameof(jar));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _tiers = tiers ?? throw new ArgumentNullException(nameof(tiers));
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _flow = new DropFlow(config.DropCooldown);
            _heldX = (jar.InteriorMin.x + jar.InteriorMax.x) * 0.5f;
        }

        /// <summary>
        /// Starts reading input and holds the queue's current piece, controllable at once. A cooldown that was in
        /// progress when the controller was disabled is cancelled, not restarted. Does nothing when already
        /// enabled. It does not advance the queue.
        /// </summary>
        /// <exception cref="InvalidOperationException">The controller was not initialized, or the tier of the queue has no definition.</exception>
        public void Enable()
        {
            ThrowIfNotInitialized();
            if (IsEnabled)
            {
                return;
            }

            var wasDropping = _flow.State == DropState.Dropping;
            _flow.Begin();
            ClearPress();
            IsEnabled = true;
            Subscribe();
            Attach();

            if (wasDropping)
            {
                StateChanged?.Invoke(DropState.Aiming);
            }
        }

        /// <summary>
        /// Stops reading input and gives the held piece back to the factory, so nothing stale is left in the jar.
        /// Pieces already dropped are not touched. <see cref="Enable"/> holds the queue's current piece again.
        /// </summary>
        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            IsEnabled = false;
            Unsubscribe();
            ClearPress();
            DiscardHeld();
        }

        /// <summary>
        /// Starts a new run: the held piece is discarded, the queue of the new run replaces the old one and the
        /// state goes back to <see cref="DropState.Aiming"/>. While enabled, a fresh piece from the new queue is held
        /// at once; while disabled, <see cref="Enable"/> will hold it. It does not release the pieces of the
        /// previous run: the owner calls <see cref="PieceFactory.ReleaseAll"/>, before or after this.
        /// </summary>
        /// <param name="queue">The spawn queue of the new run.</param>
        /// <exception cref="ArgumentNullException">The queue is null.</exception>
        /// <exception cref="InvalidOperationException">The controller was not initialized.</exception>
        public void ResetForNewRun(SpawnQueue queue)
        {
            ThrowIfNotInitialized();
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));

            DiscardHeld();
            _flow.Begin();
            ClearPress();
            if (IsEnabled)
            {
                Attach();
            }

            StateChanged?.Invoke(DropState.Aiming);
        }

        /// <summary>
        /// The per-frame work: ends the cooldown, grows the next piece and drops on a release. Public so tests can
        /// drive it with an exact time step.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last call.</param>
        public void Tick(float deltaTime)
        {
            if (!IsEnabled)
            {
                return;
            }

            // The factory may have taken the held piece back, or a failed drop may have left none: hold a new one.
            if (!HasValidHeld())
            {
                _held = null;
                Attach();
            }

            if (_flow.Tick(deltaTime))
            {
                StateChanged?.Invoke(DropState.Aiming);
            }

            // The flags are cleared before anything can throw, so a failure never leaves a press behind.
            var pressed = _pressQueued;
            var released = _releaseQueued;
            var cancelled = _cancelQueued;
            ClearQueuedInput();

            if (pressed)
            {
                _flow.Press();
            }

            // A cancelled press goes back to hovering: it is forgotten and never drops.
            if (cancelled)
            {
                _flow.CancelPress();
                released = false;
            }

            if (cancelled || _flow.State != DropState.Aiming)
            {
                _dropPending = false;
            }

            // The release decides: a press seen at any time (even one that began during the cooldown) is enough
            // to drop once the piece can be dropped. A release with no press before it, such as the tail of a
            // tap on a button, drops nothing and does not send the piece anywhere.
            if (released && _releaseHadPress && _flow.State == DropState.Aiming)
            {
                _dropPending = true;
                _flow.Press();
            }

            // A quick tap releases before the piece has followed the finger: it keeps going to the release point and drops there.
            if (_dropPending && Mathf.Abs(_heldX - _releaseTargetX) <= REACH_EPSILON)
            {
                _dropPending = false;
                if (_flow.TryRelease())
                {
                    Drop();
                }
            }
        }

        /// <summary>
        /// The physics work: follows the input (plus the offset taken at the press) at the capped speed, kept inside
        /// the jar after the offset is added, and moves the held piece
        /// through its body. Public so tests can drive it with an exact time step.
        /// </summary>
        /// <param name="fixedDeltaTime">Seconds of the physics step.</param>
        public void FixedTick(float fixedDeltaTime)
        {
            if (!IsEnabled || !HasValidHeld() || _flow.State != DropState.Aiming)
            {
                return;
            }

            var maxSpeed = _config.MaxFollowSpeed;
            var target = _dropPending
                ? _releaseTargetX
                : _input.HasPointer ? _input.PointerWorldX + _followOffset : _heldX + _input.MoveAxis * maxSpeed * fixedDeltaTime;
            _heldX = DropFlow.Follow(_heldX, ClampToJar(target), maxSpeed, fixedDeltaTime);

            _held.Rigidbody.MovePosition(new Vector2(_heldX, _jar.DropLineY));
        }

        /// <summary>
        /// Remembers that a press began, to act on it in the next <see cref="Tick"/>, and takes the offset the input
        /// wants kept between the pointer and the piece while the press lasts.
        /// </summary>
        private void OnDropPressed()
        {
            _pressQueued = true;
            _inputDown = true;
            _dropPending = false; // A new press ends any glide to the point of an earlier release.
            _followOffset = _input.GetPointerOffset(_heldX);
            _pressFollowsPointer = _input.HasPointer;
        }

        /// <summary>
        /// Remembers that a press was cancelled, to go back to hovering in the next <see cref="Tick"/>. The offset of
        /// the press ends with it.
        /// </summary>
        private void OnDropCancelled()
        {
            _cancelQueued = true;
            _inputDown = false;
            _followOffset = 0f;
        }

        /// <summary>
        /// Remembers that a press ended, to act on it in the next <see cref="Tick"/>.
        /// </summary>
        private void OnDropReleased()
        {
            _releaseQueued = true;
            _releaseHadPress = _inputDown;
            _inputDown = false;
            _releaseTargetX = _pressFollowsPointer ? ClampToJar(_input.PointerWorldX + _followOffset) : _heldX;
            _followOffset = 0f;
        }

        /// <summary>
        /// Lets the held piece fall straight down from where it is, then holds the next one. The queue advances
        /// after the release, so the previewed piece is exactly the one that appears. The next piece is held before
        /// any event is raised, so a listener that throws cannot leave the controller without a piece.
        /// </summary>
        private void Drop()
        {
            var dropped = _held;
            var tierIndex = dropped.Tier.Index;

            dropped.SetHeld(false);
            _held = null;

            _queue.Advance();
            Attach();

            if (_flow.State == DropState.Dropping)
            {
                StateChanged?.Invoke(DropState.Dropping);
            }

            PieceDropped?.Invoke(tierIndex);
        }

        /// <summary>
        /// Creates the piece of the queue's current tier at the Drop Line and holds it. The piece pops in by itself.
        /// </summary>
        private void Attach()
        {
            var tierIndex = _queue.Current;
            if (tierIndex < 0 || tierIndex >= _tiers.Count)
            {
                throw new InvalidOperationException($"The queue asks for tier {tierIndex} but there are {_tiers.Count} tiers.");
            }

            var tier = _tiers[tierIndex];
            _heldRadius = tier.DiameterUnits * 0.5f;
            _heldX = ClampToJar(_heldX);

            _held = _factory.Create(tier, new Vector2(_heldX, _jar.DropLineY), Vector2.zero);
            _held.SetHeld(true);
        }

        /// <summary>
        /// Whether the controller still holds its piece: the factory has not taken it back and nobody else has
        /// reused it, which would leave it released.
        /// </summary>
        /// <returns>True when the held piece is active and still held.</returns>
        private bool HasValidHeld()
        {
            return _held != null && _held.IsHeld && _held.gameObject.activeSelf;
        }

        /// <summary>
        /// Gives the held piece back to the factory, unless it was already taken back, for instance by
        /// <see cref="PieceFactory.ReleaseAll"/>, or reused by someone else.
        /// </summary>
        private void DiscardHeld()
        {
            if (HasValidHeld())
            {
                _factory.Release(_held);
            }

            _held = null;
        }

        /// <summary>
        /// Keeps a piece of the held radius inside the jar interior.
        /// </summary>
        /// <param name="x">Wanted centre of the piece.</param>
        /// <returns>The centre, kept inside.</returns>
        private float ClampToJar(float x)
        {
            return DropFlow.ClampX(x, _heldRadius, _jar.InteriorMin.x, _jar.InteriorMax.x);
        }

        /// <summary>
        /// Forgets the presses and releases noted but not yet acted on.
        /// </summary>
        private void ClearQueuedInput()
        {
            _pressQueued = false;
            _releaseQueued = false;
            _cancelQueued = false;
        }

        /// <summary>
        /// Forgets the queued input and that a finger or key is down, for when the controller starts, stops or
        /// restarts. <see cref="Tick"/> calls <see cref="ClearQueuedInput"/> alone, because the press it just read
        /// may still be down.
        /// </summary>
        private void ClearPress()
        {
            ClearQueuedInput();
            _inputDown = false;
        }

        /// <summary>
        /// Listens to the input. Safe to call when already listening.
        /// </summary>
        private void Subscribe()
        {
            Unsubscribe();
            _input.DropPressed += OnDropPressed;
            _input.DropReleased += OnDropReleased;
            _input.DropCancelled += OnDropCancelled;
        }

        /// <summary>
        /// Stops listening to the input.
        /// </summary>
        private void Unsubscribe()
        {
            if (_input != null)
            {
                _input.DropPressed -= OnDropPressed;
                _input.DropReleased -= OnDropReleased;
                _input.DropCancelled -= OnDropCancelled;
            }
        }

        /// <summary>
        /// Fails clearly when the controller is used before <see cref="Initialize"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The controller was not initialized.</exception>
        private void ThrowIfNotInitialized()
        {
            if (_flow == null)
            {
                throw new InvalidOperationException("DropController.Initialize must be called first.");
            }
        }
    }
}
