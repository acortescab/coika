using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Coika.Gameplay
{
    /// <summary>
    /// Reads the mouse, the touch screen and the keyboard with the Input System and offers them as an
    /// <see cref="IDropInput"/> (S-71: the only place that reads devices). The pointer follows
    /// <c>&lt;Pointer&gt;/position</c> and presses with <c>&lt;Pointer&gt;/press</c>, which unifies mouse and touch;
    /// A/D and the arrow keys move and Space drops. A press that begins over the UI never drops.
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
        private bool _pressPending;
        private bool _releasePending;
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
        public bool HasPointer => _state.HasPointer;

        /// <inheritdoc />
        public float PointerWorldX => _state.PointerWorldX;

        /// <inheritdoc />
        public float MoveAxis => _state.MoveAxis;

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

            _state.PointerMoved(world.x, isInside);
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
        /// A pointer press ended. It is passed on in <see cref="Update"/>, after the press that began it.
        /// </summary>
        /// <param name="context">Details of the action. Not used.</param>
        private void OnPressCanceled(InputAction.CallbackContext context)
        {
            _releasePending = true;
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
                _state.PointerPressed(IsPointerOverUi());
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
