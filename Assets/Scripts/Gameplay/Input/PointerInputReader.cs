using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Coika.Gameplay
{
    /// <summary>
    /// Reads the mouse, the touch screen and the keyboard with the Input System and offers them as an
    /// <see cref="IDropInput"/> (S-71: the only place that reads devices). The pointer follows
    /// the mouse or the primary touch only (a second finger is ignored) and presses with the mouse button or that
    /// touch; A/D and the arrow keys move, Space drops and Escape (the Android Back button) raises
    /// <see cref="BackPressed"/>. A press that begins over the UI never drops, and a touch the system cancels
    /// never drops. A finger keeps the offset the piece had when it went down, or the Finger Offset side set with
    /// <see cref="ConfigureFingerOffset"/>.
    /// <para>
    /// It works on its own copy of the action asset, which it destroys with itself (S-72). The decisions (last
    /// device used, UI presses, focus loss) are made by <see cref="DropInputState"/>.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/Input/Pointer Input Reader")]
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class PointerInputReader : MonoBehaviour, IDropInput
    {
        /// <summary>Name of the action map inside the asset.</summary>
        public const string MAP_NAME = "Gameplay";

        private const string POINT_ACTION = "Point";
        private const string PRESS_ACTION = "Press";
        private const string MOVE_ACTION = "Move";
        private const string DROP_ACTION = "Drop";
        private const string BACK_ACTION = "Back";

        [SerializeField]
        private Camera _camera;
        [SerializeField]
        private InputActionAsset _actions;

        private readonly DropInputState _state = new();

        private InputActionAsset _runtimeActions;
        private InputActionMap _map;
        private InputAction _point;
        private InputAction _press;
        private InputAction _move;
        private InputAction _drop;
        private InputAction _back;
        private bool _pressPending;
        private bool _releasePending;
        private bool _cancelPending;
        private bool _pressIsTouch;
        private int _pressTouchId;

        /// <inheritdoc />
        public event Action DropPressed
        {
            add => _state.DropPressed += value;
            remove => _state.DropPressed -= value;
        }

        /// <inheritdoc />
        public event Action DropReleased
        {
            add => _state.DropReleased += value;
            remove => _state.DropReleased -= value;
        }

        /// <inheritdoc />
        public event Action DropCancelled
        {
            add => _state.DropCancelled += value;
            remove => _state.DropCancelled -= value;
        }

        /// <inheritdoc />
        public event Action BackPressed
        {
            add => _state.BackPressed += value;
            remove => _state.BackPressed -= value;
        }

        /// <inheritdoc />
        public bool HasPointer => _state.HasPointer;

        /// <inheritdoc />
        public float PointerWorldX => _state.PointerWorldX;

        /// <inheritdoc />
        public float MoveAxis => _state.MoveAxis;

        /// <inheritdoc />
        public float GetPointerOffset(float heldX)
        {
            return _state.GetPointerOffset(heldX);
        }

        /// <summary>
        /// Sets where the held piece sits relative to the finger (the Finger Offset and Left-handed settings).
        /// </summary>
        /// <param name="enabled">Whether the Finger Offset setting is on.</param>
        /// <param name="leftHanded">Whether the Left-handed setting is on.</param>
        /// <param name="distance">Sideways distance in world units from <c>GameConfig.FingerOffset</c>.</param>
        public void ConfigureFingerOffset(bool enabled, bool leftHanded, float distance)
        {
            _state.ConfigureFingerOffset(enabled, leftHanded, distance);
        }

        /// <summary>
        /// Whether the game window has focus. A release that happens without focus never drops. It is virtual only
        /// so that tests, which run without a focused window, can say it has.
        /// </summary>
        protected virtual bool HasFocus => Application.isFocused;

        /// <summary>
        /// Makes this reader's own copy of the actions, so several readers never share enabled state.
        /// </summary>
        private void Awake()
        {
            if (_camera == null || _actions == null)
            {
                Debug.LogError("PointerInputReader needs a Camera and the Input Actions asset.", this);
                enabled = false;
                return;
            }

            _runtimeActions = Instantiate(_actions);
            _map = _runtimeActions.FindActionMap(MAP_NAME, true);
            _point = _map.FindAction(POINT_ACTION, true);
            _press = _map.FindAction(PRESS_ACTION, true);
            _move = _map.FindAction(MOVE_ACTION, true);
            _drop = _map.FindAction(DROP_ACTION, true);
            _back = _map.FindAction(BACK_ACTION, true);
        }

        /// <summary>
        /// Enables the actions and listens to the presses (S-72).
        /// </summary>
        private void OnEnable()
        {
            if (_map == null)
            {
                return;
            }

            _press.started += OnPressStarted;
            _press.canceled += OnPressCanceled;
            _drop.started += OnDropStarted;
            _drop.canceled += OnDropCanceled;
            _back.started += OnBackStarted;
            _map.Enable();
        }

        /// <summary>
        /// Stops listening, disables the actions and forgets any press in progress.
        /// </summary>
        private void OnDisable()
        {
            if (_map == null)
            {
                return;
            }

            _map.Disable();
            _press.started -= OnPressStarted;
            _press.canceled -= OnPressCanceled;
            _drop.started -= OnDropStarted;
            _drop.canceled -= OnDropCanceled;
            _back.started -= OnBackStarted;
            ClearPendingPress();
            _state.Cancel();
        }

        /// <summary>
        /// Destroys the copy of the actions this reader made.
        /// </summary>
        private void OnDestroy()
        {
            if (_runtimeActions != null)
            {
                Destroy(_runtimeActions);
            }
        }

        /// <summary>
        /// Reads the pointer position and the keyboard axis every frame. The camera is cached, never searched (S-21).
        /// </summary>
        private void Update()
        {
            var screen = _point.ReadValue<Vector2>();
            var depth = Mathf.Abs(_camera.transform.position.z);
            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            var isInside = screen.x >= 0f && screen.y >= 0f && screen.x < Screen.width && screen.y < Screen.height;

            var touchscreen = Touchscreen.current;
            var fromTouch = touchscreen != null && touchscreen.primaryTouch.isInProgress;
            _state.PointerMoved(world.x, isInside, fromTouch);
            _state.KeyboardAxisChanged(_move.ReadValue<float>());
            ProcessPointerPress();
        }

        /// <summary>
        /// A press that leaves the window must not drop: forget the presses in progress when focus is lost.
        /// </summary>
        /// <param name="hasFocus">Whether the window has focus now.</param>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ClearPendingPress();
                _state.Cancel();
            }
        }

        /// <summary>
        /// A pointer press began. It is only noted here: the event system must not be asked from inside an input
        /// callback, because it would answer with the state of the previous frame. <see cref="Update"/> asks it.
        /// The id of the finger is read now, because a quick tap is over before the next <see cref="Update"/>.
        /// </summary>
        /// <param name="context">Details of the action. Not used.</param>
        private void OnPressStarted(InputAction.CallbackContext context)
        {
            var touchscreen = Touchscreen.current;
            _pressIsTouch = touchscreen != null && touchscreen.primaryTouch.isInProgress;
            _pressTouchId = _pressIsTouch ? touchscreen.primaryTouch.touchId.ReadValue() : 0;
            _pressPending = true;
        }

        /// <summary>
        /// A pointer press ended. It is passed on in <see cref="Update"/>, after the press that began it. A finger
        /// that the system took away (phase Canceled) is told apart from one that was lifted: it never drops.
        /// </summary>
        /// <param name="context">Details of the action. Not used.</param>
        private void OnPressCanceled(InputAction.CallbackContext context)
        {
            var touchscreen = Touchscreen.current;
            if (_pressIsTouch && touchscreen != null && touchscreen.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                _cancelPending = true;
                return;
            }

            _releasePending = true;
        }

        /// <summary>
        /// The Back key (Escape, or the Android Back button) was pressed.
        /// </summary>
        /// <param name="context">Details of the action. Not used.</param>
        private void OnBackStarted(InputAction.CallbackContext context)
        {
            _state.BackKeyPressed();
        }

        /// <summary>
        /// Passes the pointer press and release noted by the callbacks to the state, in order, asking the event
        /// system whether the press began over the UI.
        /// </summary>
        private void ProcessPointerPress()
        {
            if (_pressPending)
            {
                _pressPending = false;
                _state.PointerPressed(IsPointerOverUi(), _pressIsTouch);
            }

            if (_cancelPending)
            {
                _cancelPending = false;
                _releasePending = false;
                _state.PointerCancelled();
            }

            if (_releasePending)
            {
                _releasePending = false;
                _state.PointerReleased(HasFocus);
            }
        }

        /// <summary>
        /// Forgets the presses noted but not yet passed on.
        /// </summary>
        private void ClearPendingPress()
        {
            _pressPending = false;
            _releasePending = false;
            _cancelPending = false;
        }

        /// <summary>
        /// The drop key was pressed.
        /// </summary>
        /// <param name="context">Details of the action. Not used.</param>
        private void OnDropStarted(InputAction.CallbackContext context)
        {
            _state.KeyboardDropPressed();
        }

        /// <summary>
        /// The drop key was released.
        /// </summary>
        /// <param name="context">Details of the action. Not used.</param>
        private void OnDropCanceled(InputAction.CallbackContext context)
        {
            _state.KeyboardDropReleased(HasFocus);
        }

        /// <summary>
        /// Whether the press being passed on began over a UI element. A touch needs the id of the finger, because
        /// the call without it only knows the mouse. With no event system in the scene nothing is over the UI.
        /// </summary>
        /// <returns>True when the press began over the UI.</returns>
        private bool IsPointerOverUi()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            return _pressIsTouch ? eventSystem.IsPointerOverGameObject(_pressTouchId) : eventSystem.IsPointerOverGameObject();
        }
    }
}
