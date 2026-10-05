using System;
using Coika.Core.Animation;
using Coika.Data;
using UnityEngine;

namespace Coika.Gameplay
{
    /// <summary>
    /// Visual-only clones of the two pieces of a merge. The real pieces are released in the same physics step (the
    /// merge stays instantaneous and deterministic), so the player would see them vanish; each ghost shows the old
    /// sprite at the old position and shrinks it to nothing. A ghost has no body, no collider and no layer
    /// interaction. The ghosts are created once up front and recycled, oldest first when all are busy, so nothing is
    /// allocated or instantiated during play.
    /// </summary>
    public class MergeGhostPool : MonoBehaviour
    {
        private const int SORTING_ORDER = 2;

        private Transform[] _transforms;
        private SpriteRenderer[] _renderers;
        private float[] _elapsed;
        private bool[] _active;
        private Vector3[] _startScale;
        private FeedbackConfig _config;
        private MergeSystem _merge;
        private Action<Piece, Piece> _onPairMerging;
        private int _next;

        /// <summary>Number of ghosts that are currently shrinking.</summary>
        public int ActiveCount
        {
            get
            {
                var count = 0;
                if (_active == null)
                {
                    return 0;
                }

                for (var i = 0; i < _active.Length; i++)
                {
                    if (_active[i])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Creates the ghosts and starts listening to the merge system. Call it once, before the first merge.
        /// </summary>
        /// <param name="merge">Source of the <see cref="MergeSystem.PairMerging"/> event.</param>
        /// <param name="config">Tuning of the shrink and the ghost count.</param>
        /// <exception cref="ArgumentNullException">A dependency is null.</exception>
        public void Initialize(MergeSystem merge, FeedbackConfig config)
        {
            var newMerge = merge != null ? merge : throw new ArgumentNullException(nameof(merge));
            var newConfig = config != null ? config : throw new ArgumentNullException(nameof(config));

            Unsubscribe();
            _merge = newMerge;
            _config = newConfig;
            Build(newConfig.MergeGhostPoolSize);
            _onPairMerging ??= OnPairMerging;
            _merge.PairMerging += _onPairMerging;
        }

        /// <summary>
        /// Hides every ghost, so a new run starts without leftovers.
        /// </summary>
        public void ResetAll()
        {
            if (_active == null)
            {
                return;
            }

            for (var i = 0; i < _active.Length; i++)
            {
                Hide(i);
            }
        }

        /// <summary>
        /// Advances the shrink of every active ghost.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last call.</param>
        public void Tick(float deltaTime)
        {
            if (_active == null)
            {
                return;
            }

            for (var i = 0; i < _active.Length; i++)
            {
                if (!_active[i])
                {
                    continue;
                }

                _elapsed[i] += deltaTime;
                var t = Tween.Progress(_elapsed[i], _config.MergeShrinkDuration);
                if (t >= 1f)
                {
                    Hide(i);
                    continue;
                }

                _transforms[i].localScale = _startScale[i] * (1f - Tween.OutQuad(t));
            }
        }

        /// <summary>
        /// Stops listening to the merge system.
        /// </summary>
        private void OnDestroy()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Advances the ghosts with the frame time.
        /// </summary>
        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Creates the inactive ghost objects.
        /// </summary>
        /// <param name="count">Number of ghosts.</param>
        private void Build(int count)
        {
            DestroyGhosts();
            _transforms = new Transform[count];
            _renderers = new SpriteRenderer[count];
            _elapsed = new float[count];
            _active = new bool[count];
            _startScale = new Vector3[count];
            _next = 0;

            for (var i = 0; i < count; i++)
            {
                var ghost = new GameObject("MergeGhost");
                ghost.transform.SetParent(transform, false);
                var spriteRenderer = ghost.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = SORTING_ORDER;
                ghost.SetActive(false);
                _transforms[i] = ghost.transform;
                _renderers[i] = spriteRenderer;
            }
        }

        /// <summary>
        /// Destroys the ghosts of a previous <see cref="Initialize"/>.
        /// </summary>
        private void DestroyGhosts()
        {
            if (_transforms == null)
            {
                return;
            }

            for (var i = 0; i < _transforms.Length; i++)
            {
                if (_transforms[i] != null)
                {
                    Destroy(_transforms[i].gameObject);
                }
            }
        }

        /// <summary>
        /// Starts a ghost for each source piece of a merge.
        /// </summary>
        /// <param name="low">First source piece.</param>
        /// <param name="high">Second source piece.</param>
        private void OnPairMerging(Piece low, Piece high)
        {
            Show(low);
            Show(high);
        }

        /// <summary>
        /// Copies the sprite, position and rotation of a piece onto the next ghost and starts its shrink.
        /// </summary>
        /// <param name="piece">The piece that is about to be released.</param>
        private void Show(Piece piece)
        {
            var i = _next;
            _next = (_next + 1) % _transforms.Length;

            _renderers[i].sprite = piece.Sprite;
            _transforms[i].SetPositionAndRotation(piece.transform.position, piece.transform.rotation);
            _startScale[i] = Vector3.one;
            _transforms[i].localScale = Vector3.one;
            _elapsed[i] = 0f;
            _active[i] = true;
            _transforms[i].gameObject.SetActive(true);
        }

        /// <summary>
        /// Deactivates one ghost.
        /// </summary>
        /// <param name="index">Index of the ghost.</param>
        private void Hide(int index)
        {
            _active[index] = false;
            _transforms[index].gameObject.SetActive(false);
        }

        /// <summary>
        /// Removes the listener from the merge system.
        /// </summary>
        private void Unsubscribe()
        {
            if (_merge != null && _onPairMerging != null)
            {
                _merge.PairMerging -= _onPairMerging;
            }
        }
    }
}
