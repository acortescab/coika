using Coika.Data;
using Coika.Fx;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Checks the two components of issue #33 that touch the scene, <see cref="ScreenShake"/> and
    /// <see cref="ScreenFlash"/>, on a fake clock: the rig moves in whole 1/16 steps and returns exactly to rest, the
    /// camera inside it keeps its own position, the overlay fades and hides, and nothing allocates per frame.
    /// </summary>
    public class ScreenShakePlayModeTests
    {
        private const float STEP = 1f / 16f;
        private const double FRAME = 1.0 / 60.0;
        private const int FRAMES = 600;

        private FeedbackConfig _config;
        private GameObject _rigObject;
        private GameObject _cameraObject;
        private ScreenShake _shake;
        private double _now;

        /// <summary>
        /// Builds a rig away from the origin with a camera child at a framed position, and a shake on a fake clock.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<FeedbackConfig>();
            _rigObject = new GameObject("Rig");
            _rigObject.transform.position = new Vector3(1f, 2f, 0f);
            _cameraObject = new GameObject("Camera");
            _cameraObject.transform.SetParent(_rigObject.transform, false);
            _cameraObject.transform.localPosition = new Vector3(0.5f, 3f, -10f);
            _shake = _rigObject.AddComponent<ScreenShake>();
            _now = 10.0;
            _shake.Initialize(_config, () => _now);
        }

        /// <summary>
        /// Destroys what the test built.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rigObject);
            Object.DestroyImmediate(_config);
        }

        /// <summary>
        /// While shaking, the rig is a whole number of 1/16 steps from its rest position; after the shake it is back at
        /// the rest position exactly, and the camera's position inside the rig never changed.
        /// </summary>
        [Test]
        public void Shake_ThenWaiting_SnapsToSteps_AndReturnsExactlyToRest()
        {
            var rest = _rigObject.transform.position;
            var cameraLocal = _cameraObject.transform.localPosition;
            _shake.Shake(0.15f, 0.2f);

            for (var i = 0; i < 20; i++)
            {
                _now += FRAME;
                InvokeLateUpdate();
                var offset = _rigObject.transform.position - rest;
                Assert.AreEqual(Mathf.Round(offset.x / STEP) * STEP, offset.x, 0.00001f);
                Assert.AreEqual(Mathf.Round(offset.y / STEP) * STEP, offset.y, 0.00001f);
                Assert.AreEqual(0f, offset.z);
            }

            _now += 1.0;
            InvokeLateUpdate();

            Assert.AreEqual(rest, _rigObject.transform.position);
            Assert.AreEqual(cameraLocal, _cameraObject.transform.localPosition);
        }

        /// <summary>
        /// Clearing in the middle of a shake puts the rig back at rest at once.
        /// </summary>
        [Test]
        public void Clear_DuringAShake_PutsTheRigAtRest()
        {
            var rest = _rigObject.transform.position;
            _shake.Shake(0.3f, 1f);
            _now += 0.05;
            InvokeLateUpdate();

            _shake.Clear();

            Assert.AreEqual(rest, _rigObject.transform.position);
        }

        /// <summary>
        /// A frame of shake and a frame of flash allocate nothing.
        /// </summary>
        [Test]
        public void LateUpdate_WhileShakingAndFlashing_AllocatesNothing()
        {
            var flashObject = new GameObject("Flash", typeof(CanvasGroup));
            var flash = flashObject.AddComponent<ScreenFlash>();
            TestReflection.SetField(flash, "_group", flashObject.GetComponent<CanvasGroup>());
            flash.Initialize(_config, () => _now);
            var flashUpdate = TestReflection.GetAction(flash, "LateUpdate");
            var shakeUpdate = TestReflection.GetAction(_shake, "LateUpdate");
            _shake.Shake(0.2f, 100f);
            flash.Flash(100f);

            var allocations = AllocationMeter.Measure(() =>
            {
                for (var i = 0; i < FRAMES; i++)
                {
                    _now += FRAME;
                    shakeUpdate();
                    flashUpdate();
                }
            });

            Object.DestroyImmediate(flashObject);
            Assert.LessOrEqual(allocations, AllocationMeter.TOLERANCE_COUNT);
        }

        /// <summary>
        /// The overlay starts at the peak, fades out, hides, and ignores a second request that would make the
        /// flashes faster than 3 Hz.
        /// </summary>
        [Test]
        public void Flash_Requested_FadesAndLimitsTheRate()
        {
            var flashObject = new GameObject("Flash", typeof(CanvasGroup));
            var group = flashObject.GetComponent<CanvasGroup>();
            var flash = flashObject.AddComponent<ScreenFlash>();
            TestReflection.SetField(flash, "_group", group);
            flash.Initialize(_config, () => _now);

            flash.Flash(0.15f);
            TestReflection.GetAction(flash, "LateUpdate")();
            Assert.AreEqual(_config.ScreenFx.ScreenFlashPeakAlpha, group.alpha, 0.0001f);

            _now += 0.1;
            flash.Flash(0.15f);
            _now += 0.06;
            TestReflection.GetAction(flash, "LateUpdate")();
            Assert.AreEqual(0f, group.alpha, "The second request was ignored, so the first flash is over.");

            Object.DestroyImmediate(flashObject);
        }

        /// <summary>
        /// Runs the frame update of the shake, which Unity would call after every Update.
        /// </summary>
        private void InvokeLateUpdate()
        {
            TestReflection.GetAction(_shake, "LateUpdate")();
        }
    }
}
