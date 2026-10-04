#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Runs the <see cref="PointerInputReader"/> (issue #6) against virtual devices: the pointer is followed, a
    /// click and the Space key raise the press and the release, the keyboard moves, a press that begins over the UI
    /// never drops, and a press that ends because the window lost focus never drops either. The input system is
    /// isolated by <see cref="InputTestFixture"/>, so the real devices of the Editor are not used.
    /// </summary>
    public class PointerInputReaderPlayModeTests : InputTestFixture
    {
        private const string ACTIONS_PATH = "Assets/Input/Coika.inputactions";

        private readonly List<Object> _created = new();
        private Mouse _mouse;
        private Keyboard _keyboard;
        private Touchscreen _touchscreen;
        private Camera _camera;
        private PointerInputReader _reader;
        private int _pressed;
        private int _released;
        private int _cancelled;
        private int _back;

        /// <summary>
        /// Creates the virtual mouse and keyboard, a camera and a reader that has focus.
        /// </summary>
        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _touchscreen = InputSystem.AddDevice<Touchscreen>();

            var cameraObject = new GameObject("Camera");
            _created.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 10f;

            var readerObject = new GameObject("Reader");
            _created.Add(readerObject);
            readerObject.SetActive(false);
            _reader = readerObject.AddComponent<FocusedReader>();
            SetField(_reader, "_camera", _camera);
            SetField(_reader, "_actions", UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(ACTIONS_PATH));
            readerObject.SetActive(true);

            _pressed = 0;
            _released = 0;
            _cancelled = 0;
            _back = 0;
            _reader.DropPressed += () => _pressed++;
            _reader.DropReleased += () => _released++;
            _reader.DropCancelled += () => _cancelled++;
            _reader.BackPressed += () => _back++;
        }

        /// <summary>
        /// Destroys what the test created and restores the input system.
        /// </summary>
        public override void TearDown()
        {
            foreach (var created in _created)
            {
                Object.Destroy(created);
            }

            _created.Clear();
            base.TearDown();
        }

        /// <summary>
        /// After the pointer moves inside the screen it is followed, and its world X is the one under it.
        /// </summary>
        [UnityTest]
        public IEnumerator Pointer_AfterMovingInsideTheScreen_IsFollowedAtTheWorldXUnderIt()
        {
            yield return null;
            Assert.IsFalse(_reader.HasPointer, "A pointer that has not moved is not followed.");

            var moved = new Vector2(Screen.width * 0.5f + 100f, Screen.height * 0.5f);
            Set(_mouse.position, moved);
            yield return null;

            Assert.IsTrue(_reader.HasPointer);
            Assert.AreEqual(_camera.ScreenToWorldPoint(new Vector3(moved.x, moved.y, 10f)).x, _reader.PointerWorldX, 0.01f);
        }

        /// <summary>
        /// A pointer outside the screen is not followed.
        /// </summary>
        [UnityTest]
        public IEnumerator Pointer_OutsideTheScreen_IsNotFollowed()
        {
            Set(_mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            Set(_mouse.position, new Vector2(Screen.width * 0.5f + 50f, Screen.height * 0.5f));
            yield return null;

            Set(_mouse.position, new Vector2(Screen.width + 500f, Screen.height * 0.5f));
            yield return null;

            Assert.IsFalse(_reader.HasPointer);
        }

        /// <summary>
        /// A click raises the press and then the release, once each.
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WithTheMouse_RaisesPressAndRelease()
        {
            Set(_mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;

            Press(_mouse.leftButton);
            yield return null;
            Assert.AreEqual(1, _pressed);
            Assert.AreEqual(0, _released);

            Release(_mouse.leftButton);
            yield return null;

            Assert.AreEqual(1, _released);
        }

        /// <summary>
        /// Space drops like a click.
        /// </summary>
        [UnityTest]
        public IEnumerator Space_WhenPressedAndReleased_RaisesPressAndRelease()
        {
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            yield return null;

            Assert.AreEqual(1, _pressed);
            Assert.AreEqual(1, _released);
        }

        /// <summary>
        /// A, D and the arrows move: the axis follows the keys, the keyboard becomes the last device, and moving
        /// the pointer hands the control back.
        /// </summary>
        [UnityTest]
        public IEnumerator Keyboard_WithTheMoveKeys_SetsTheAxisAndYieldsToThePointerWhenItMoves()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Set(_mouse.position, center);
            yield return null;
            Set(_mouse.position, center + new Vector2(10f, 0f));
            yield return null;
            Assert.IsTrue(_reader.HasPointer);

            Press(_keyboard.dKey);
            yield return null;
            Assert.AreEqual(1f, _reader.MoveAxis);
            Assert.IsFalse(_reader.HasPointer, "The keyboard is the last device.");
            Release(_keyboard.dKey);
            yield return null;

            Press(_keyboard.leftArrowKey);
            yield return null;
            Assert.AreEqual(-1f, _reader.MoveAxis);
            Release(_keyboard.leftArrowKey);

            Set(_mouse.position, center + new Vector2(60f, 0f));
            yield return null;
            Assert.IsTrue(_reader.HasPointer, "The pointer took the control back.");
            Assert.AreEqual(0f, _reader.MoveAxis);
        }

        /// <summary>
        /// A press that begins over a UI element raises nothing, neither when it begins nor when it ends.
        /// </summary>
        [UnityTest]
        public IEnumerator Click_OverAUiElement_NeverDrops()
        {
            CreateFullScreenUiElement();
            Set(_mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            yield return null;
            yield return null;

            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;

            Assert.IsTrue(EventSystem.current.IsPointerOverGameObject(), "The test set-up must put the pointer over the UI.");
            Assert.AreEqual(0, _pressed);
            Assert.AreEqual(0, _released);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// A press that ends because the window lost focus does not drop.
        /// </summary>
        [UnityTest]
        public IEnumerator Click_WhenTheWindowLosesFocusMidPress_DoesNotDrop()
        {
            Set(_mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Assert.AreEqual(1, _pressed);

            typeof(PointerInputReader)
                .GetMethod("OnApplicationFocus", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_reader, new object[] { false });
            Release(_mouse.leftButton);
            yield return null;

            Assert.AreEqual(0, _released);
        }

        /// <summary>
        /// A finger that goes down presses, is followed while it is down, and drops when it is lifted.
        /// </summary>
        [UnityTest]
        public IEnumerator Touch_DownMoveAndUp_PressesFollowsAndReleases()
        {
            var down = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            BeginTouch(1, down);
            yield return null;
            Assert.AreEqual(1, _pressed);
            Assert.IsTrue(_reader.HasPointer);

            var moved = down + new Vector2(80f, 0f);
            MoveTouch(1, moved);
            yield return null;
            Assert.AreEqual(_camera.ScreenToWorldPoint(new Vector3(moved.x, moved.y, 10f)).x, _reader.PointerWorldX, 0.01f);

            EndTouch(1, moved);
            yield return null;

            Assert.AreEqual(1, _released);
            Assert.AreEqual(0, _cancelled);
            Assert.IsFalse(_reader.HasPointer, "A finger that is up is not followed.");
        }

        /// <summary>
        /// The Finger Offset and Left-handed settings reach the state: a finger sits beside the piece, on the side
        /// the hand chooses.
        /// </summary>
        [UnityTest]
        public IEnumerator Touch_WithTheFingerOffsetConfigured_ReportsTheOffsetOfTheHand()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            _reader.ConfigureFingerOffset(true, false, 2f);
            BeginTouch(1, center);
            yield return null;
            Assert.AreEqual(-2f, _reader.GetPointerOffset(5f), "Right-handed: the piece is left of the finger.");
            EndTouch(1, center);
            yield return null;

            _reader.ConfigureFingerOffset(true, true, 2f);
            BeginTouch(2, center);
            yield return null;
            Assert.AreEqual(2f, _reader.GetPointerOffset(5f), "Left-handed: the piece is right of the finger.");
            EndTouch(2, center);
            yield return null;

            _reader.ConfigureFingerOffset(false, false, 2f);
            BeginTouch(3, center);
            yield return null;
            Assert.AreEqual(0f, _reader.GetPointerOffset(5f), 0.01f, "Off: the piece heads to the finger.");
        }

        /// <summary>
        /// Only the primary touch counts: a second finger neither moves the piece nor presses nor releases.
        /// </summary>
        [UnityTest]
        public IEnumerator Touch_SecondFinger_IsIgnored()
        {
            var first = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var second = first + new Vector2(150f, 0f);
            BeginTouch(1, first);
            yield return null;
            var firstWorldX = _reader.PointerWorldX;

            BeginTouch(2, second);
            yield return null;
            MoveTouch(2, second + new Vector2(40f, 0f));
            yield return null;
            Assert.AreEqual(1, _pressed, "The second finger does not press.");
            Assert.AreEqual(firstWorldX, _reader.PointerWorldX, 0.01f, "The second finger does not move the target.");

            EndTouch(2, second + new Vector2(40f, 0f));
            yield return null;
            Assert.AreEqual(0, _released, "Lifting the second finger does not drop.");

            EndTouch(1, first);
            yield return null;
            Assert.AreEqual(1, _released);
        }

        /// <summary>
        /// A touch the system cancels never drops: the reader raises the cancel event instead.
        /// </summary>
        [UnityTest]
        public IEnumerator Touch_Cancelled_RaisesCancelAndNeverDrops()
        {
            BeginTouch(1, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            Assert.AreEqual(1, _pressed);

            CancelTouch(1, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;

            Assert.AreEqual(1, _cancelled);
            Assert.AreEqual(0, _released);
        }

        /// <summary>
        /// A touch that begins over a UI element raises nothing, neither when it begins nor when it ends.
        /// </summary>
        [UnityTest]
        public IEnumerator Touch_BeginningOverAUiElement_NeverDrops()
        {
            CreateFullScreenUiElement();
            yield return null;
            yield return null;

            BeginTouch(1, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            EndTouch(1, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;

            Assert.AreEqual(0, _pressed);
            Assert.AreEqual(0, _released);
            Assert.IsFalse(_reader.HasPointer);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// Documented rule: only where a press begins decides. A touch that begins on the play area and ends over the
        /// UI drops when it is lifted.
        /// </summary>
        [UnityTest]
        public IEnumerator Touch_BeginningOnThePlayAreaAndEndingOverTheUi_DropsOnRelease()
        {
            var start = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            BeginTouch(1, start);
            yield return null;

            CreateFullScreenUiElement();
            yield return null;
            MoveTouch(1, start + new Vector2(20f, 0f));
            yield return null;
            EndTouch(1, start + new Vector2(20f, 0f));
            yield return null;

            Assert.AreEqual(1, _pressed);
            Assert.AreEqual(1, _released);
        }

        /// <summary>
        /// Escape, which is also what the Android Back button sends, raises the event exactly once per press.
        /// </summary>
        [UnityTest]
        public IEnumerator Back_Escape_RaisesBackPressedOncePerPress()
        {
            Press(_keyboard.escapeKey);
            yield return null;
            yield return null;
            Assert.AreEqual(1, _back, "Holding the key raises it once.");

            Release(_keyboard.escapeKey);
            yield return null;
            Press(_keyboard.escapeKey);
            yield return null;

            Assert.AreEqual(2, _back);
            Assert.AreEqual(0, _pressed, "Back is not a drop.");
        }

        /// <summary>
        /// Reading a moving finger every frame allocates no managed memory.
        /// </summary>
        [UnityTest]
        public IEnumerator Update_WithAMovingFinger_AllocatesNoManagedMemory()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            BeginTouch(1, center);
            yield return null;
            var update = (System.Action)typeof(PointerInputReader)
                .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
                .CreateDelegate(typeof(System.Action), _reader);
            update();

            var allocated = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < 100; i++)
                {
                    update();
                }
            });

            Assert.LessOrEqual(allocated, AllocationMeter.TOLERANCE_COUNT, "Managed allocations in 100 frames of input.");
        }

        /// <summary>
        /// Creates an event system and a canvas with an image that covers the whole screen, so every point is over the UI.
        /// </summary>
        private void CreateFullScreenUiElement()
        {
            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            _created.Add(eventSystemObject);
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            _created.Add(canvasObject);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var image = new GameObject("Blocker", typeof(Image));
            image.transform.SetParent(canvasObject.transform, false);
            var rect = image.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Sets a private instance field declared in <see cref="PointerInputReader"/> by reflection.
        /// </summary>
        /// <param name="target">The reader.</param>
        /// <param name="fieldName">Name of the private field.</param>
        /// <param name="value">Value to assign.</param>
        private static void SetField(object target, string fieldName, object value)
        {
            typeof(PointerInputReader)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        /// <summary>
        /// A reader that always has focus, because the tests run without a focused window.
        /// </summary>
        private sealed class FocusedReader : PointerInputReader
        {
            /// <inheritdoc />
            protected override bool HasFocus => true;
        }
    }
}
#endif
