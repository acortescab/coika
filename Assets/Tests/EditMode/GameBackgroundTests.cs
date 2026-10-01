using System.Text.RegularExpressions;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks that the placeholder background covers the camera reference frame with room to spare (issue #3, scope 5).
    /// The GDD frame is 192 x 320 px at PPU 16, which is 12 x 20 units.
    /// </summary>
    public class GameBackgroundTests
    {
        private const float TOLERANCE = 0.0001f;

        private static readonly Vector2 ReferenceSize = new(12f, 20f);

        private GameObject _backgroundObject;
        private Texture2D _texture;
        private Sprite _sprite;

        /// <summary>
        /// Creates a background object with a 16 x 16 px sprite at PPU 16, which is exactly 1 x 1 unit.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(16, 16);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
            _backgroundObject = new GameObject("Background", typeof(SpriteRenderer), typeof(GameBackground));
        }

        /// <summary>
        /// Destroys everything the test created, so tests stay independent.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_backgroundObject);
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
        }

        /// <summary>
        /// The background is twice the reference frame in each direction.
        /// </summary>
        [Test]
        public void ComputeSize_WithReferenceFrame_ReturnsTwiceTheFrame()
        {
            var size = GameBackground.ComputeSize(ReferenceSize);

            Assert.AreEqual(24f, size.x, TOLERANCE);
            Assert.AreEqual(40f, size.y, TOLERANCE);
        }

        /// <summary>
        /// After fitting, the background is centred on the frame and covers it entirely, at least twice as tall.
        /// </summary>
        [Test]
        public void Fit_WithSprite_CentersOnFrameAndCoversItWithMargin()
        {
            var spriteRenderer = _backgroundObject.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = _sprite;
            var center = new Vector2(0f, 6.5f);

            _backgroundObject.GetComponent<GameBackground>().Fit(center, ReferenceSize);

            var bounds = spriteRenderer.bounds;
            Assert.AreEqual(center.x, bounds.center.x, TOLERANCE, "Centre x");
            Assert.AreEqual(center.y, bounds.center.y, TOLERANCE, "Centre y");
            Assert.LessOrEqual(bounds.min.x, center.x - ReferenceSize.x * 0.5f, "Covers the left edge of the frame");
            Assert.GreaterOrEqual(bounds.max.x, center.x + ReferenceSize.x * 0.5f, "Covers the right edge of the frame");
            Assert.LessOrEqual(bounds.min.y, center.y - ReferenceSize.y * 0.5f, "Covers the bottom edge of the frame");
            Assert.GreaterOrEqual(bounds.max.y, center.y + ReferenceSize.y * 0.5f, "Covers the top edge of the frame");
            Assert.GreaterOrEqual(bounds.size.y, 2f * ReferenceSize.y - TOLERANCE, "At least twice the frame height");
        }

        /// <summary>
        /// The background draws behind everything else.
        /// </summary>
        [Test]
        public void Fit_WithSprite_DrawsBehindEverything()
        {
            var spriteRenderer = _backgroundObject.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = _sprite;

            _backgroundObject.GetComponent<GameBackground>().Fit(Vector2.zero, ReferenceSize);

            Assert.Less(spriteRenderer.sortingOrder, 0);
        }

        /// <summary>
        /// Without a sprite there is nothing to fit: an error is logged and the object is left as it was.
        /// </summary>
        [Test]
        public void Fit_WithoutSprite_LogsErrorAndDoesNothing()
        {
            LogAssert.Expect(LogType.Error, new Regex("needs a sprite"));

            _backgroundObject.GetComponent<GameBackground>().Fit(new Vector2(3f, 4f), ReferenceSize);

            Assert.AreEqual(Vector3.zero, _backgroundObject.transform.position);
            Assert.AreEqual(Vector3.one, _backgroundObject.transform.localScale);
        }
    }
}
