using System;
using System.Collections.Generic;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Draws the guide line (GDD §6): a dotted vertical line from the held piece down to where it would first touch
    /// something, and a ghost of the piece, a dotted outline with nothing inside, at that landing point. It is a view: <see cref="GuideLineAim"/>
    /// decides when it shows and where the piece lands; this component moves two reusable sprite renderers and
    /// allocates nothing per frame (S-50). The line is one pixel wide and snapped to the pixel grid so it stays
    /// crisp. The ghost is a dotted circle as wide as the held piece (<see cref="GhostOutlineSprites"/>), the same size as the piece so its tier still reads (GDD §16).
    /// <para>
    /// It looks at the controller in <c>LateUpdate</c>, after the piece moved, never in an input callback. The
    /// owner gives it everything through <see cref="Initialize"/> and the Guide Line setting through
    /// <see cref="SetSettingOn"/>.
    /// </para>
    /// </summary>
    [AddComponentMenu("Coika/Guide Line View")]
    [DisallowMultipleComponent]
    public class GuideLineView : MonoBehaviour
    {
        private const float GHOST_ALPHA = 0.8f;

        [SerializeField]
        private SpriteRenderer _line;
        [SerializeField]
        private SpriteRenderer _ghost;

        private DropController _controller;
        private Jar _jar;
        private GuideLineAim _aim;
        private GhostOutlineSprites _outlines;
        private Piece _lastHeld;
        private bool _settingOn = true;
        private bool _shown;

        /// <summary>Whether the line and the ghost are being drawn.</summary>
        public bool IsShown => _shown;

        /// <summary>The ghost renderer, for tests.</summary>
        public SpriteRenderer Ghost => _ghost;

        /// <summary>The dotted line renderer, for tests.</summary>
        public SpriteRenderer Line => _line;

        /// <summary>
        /// Gives the view what it reads. Call it once; the view stays hidden until then.
        /// </summary>
        /// <param name="controller">The drop controller: the held piece and whether the player is pressing to drop.</param>
        /// <param name="jar">The jar, to know how far down to look.</param>
        /// <param name="tiers">Every tier, to draw the outline of each one now instead of while playing.</param>
        /// <exception cref="ArgumentNullException">A dependency or the tiers are null.</exception>
        /// <exception cref="InvalidOperationException">The prefab lacks a renderer, or a physics layer is not defined.</exception>
        public void Initialize(DropController controller, Jar jar, IReadOnlyList<TierDefinition> tiers)
        {
            _controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            _jar = jar != null ? jar : throw new ArgumentNullException(nameof(jar));
            if (_line == null || _ghost == null)
            {
                throw new InvalidOperationException("The guide line prefab needs a line renderer and a ghost renderer.");
            }

            var mask = LayerMask.GetMask(Piece.LAYER_NAME, JarBuilder.WALL_LAYER_NAME);
            if (mask == 0)
            {
                throw new InvalidOperationException("The Piece and Wall physics layers are not defined.");
            }

            _aim = new GuideLineAim(mask);
            _outlines?.Dispose();
            _outlines = new GhostOutlineSprites();
            _outlines.Build(tiers);
            _line.drawMode = SpriteDrawMode.Tiled;
            _ghost.color = new Color(1f, 1f, 1f, GHOST_ALPHA);
            Hide();
        }

        /// <summary>
        /// Applies the Guide Line setting at once: off hides the guide, on shows it again when a drop is possible.
        /// </summary>
        /// <param name="on">The value of the setting.</param>
        public void SetSettingOn(bool on)
        {
            _settingOn = on;
            Refresh();
        }

        /// <summary>
        /// Frees the outline textures.
        /// </summary>
        private void OnDestroy()
        {
            _outlines?.Dispose();
            _outlines = null;
        }

        /// <summary>
        /// Follows the held piece after it moved this frame.
        /// </summary>
        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>
        /// Shows, moves or hides the guide for the current state of the controller. Public so tests can drive it
        /// without a frame.
        /// </summary>
        public void Refresh()
        {
            if (_aim == null)
            {
                return;
            }

            var held = _controller.HeldPiece;
            if (!GuideLineAim.ShouldShow(_controller.IsPressing, held != null, _settingOn))
            {
                Hide();
                return;
            }

            if (held != _lastHeld)
            {
                _lastHeld = held;
                _aim.Invalidate();
                _ghost.sprite = _outlines.Get(held.Tier);
            }

            var position = (Vector2)held.transform.position;
            var radius = held.Collider.radius;
            _aim.Update(held.gameObject.scene.GetPhysicsScene2D(), position, radius, _jar.DropLineY - _jar.FloorY + radius, Time.fixedTime);
            if (!_aim.HasLanding)
            {
                Hide();
                return;
            }

            Show(position, radius, _aim.Landing);
        }

        /// <summary>
        /// Puts the ghost on the landing point and stretches the line between the piece and the ghost.
        /// </summary>
        private void Show(Vector2 position, float radius, Vector2 landing)
        {
            _ghost.transform.position = new Vector3(landing.x, landing.y, _ghost.transform.position.z);

            // The line runs from the bottom of the held piece to the top of the ghost, in whole pixels. When the two
            // touch there is nothing to draw between them.
            var top = Snap(position.y - radius);
            var length = Mathf.Floor((top - (landing.y + radius)) * TierDefinition.PixelsPerUnit) / TierDefinition.PixelsPerUnit;
            var lineVisible = length > 0f;
            _line.enabled = lineVisible;
            if (lineVisible)
            {
                _line.transform.position = new Vector3(SnapToPixelCentre(position.x), top, _line.transform.position.z);
                _line.size = new Vector2(1f / TierDefinition.PixelsPerUnit, length);
            }

            _ghost.enabled = true;
            _shown = true;
        }

        /// <summary>
        /// Hides both renderers.
        /// </summary>
        private void Hide()
        {
            if (!_shown && !_line.enabled && !_ghost.enabled)
            {
                return;
            }

            _line.enabled = false;
            _ghost.enabled = false;
            _shown = false;
            _lastHeld = null;
        }

        /// <summary>
        /// Rounds a world coordinate to a pixel boundary of the reference resolution.
        /// </summary>
        private static float Snap(float value)
        {
            return Mathf.Round(value * TierDefinition.PixelsPerUnit) / TierDefinition.PixelsPerUnit;
        }

        /// <summary>
        /// Centres a world coordinate on the pixel it falls in, so a one-pixel-wide sprite covers exactly one pixel
        /// column instead of half of two.
        /// </summary>
        private static float SnapToPixelCentre(float value)
        {
            return (Mathf.Floor(value * TierDefinition.PixelsPerUnit) + 0.5f) / TierDefinition.PixelsPerUnit;
        }
    }
}
