#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
using Coika.Core;
using Coika.Gameplay;
using Unity.Profiling;
using UnityEngine;

namespace Coika.UI
{
    /// <summary>
    /// On-screen performance readout for the Editor and development builds (issue #39): frames per second, the worst
    /// frame time and GC allocation count of the last half second, the active piece count and the device tier. The
    /// whole file is compiled out of release builds. It draws with IMGUI, so it needs no asset (C-01); S-90 allows
    /// this exception for the development overlay. Numbers come from a cache of strings, so after a short warm-up
    /// neither sampling nor drawing builds a string.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float WINDOW_SECONDS = 0.5f;
        private const int MAX_CACHED_NUMBER = 999;
        private const float LABEL_WIDTH_FACTOR = 9f; // label column width, in font sizes
        private const float VALUE_WIDTH_FACTOR = 5f;
        private const float ROW_HEIGHT_FACTOR = 1.4f;

        private readonly string[] _numbers = new string[MAX_CACHED_NUMBER + 1];

        private PieceFactory _pieces;
        private string _tierName = string.Empty;
        private ProfilerRecorder _gcAllocations;
        private GUIStyle _style;
        private float _elapsed;
        private int _frames;
        private float _maxDelta;
        private long _maxAllocations;

        // Layout of the current OnGUI pass.
        private float _left;
        private float _top;
        private float _rowHeight;
        private float _labelWidth;
        private float _valueWidth;
        private int _row;

        /// <summary>Gets the frames per second averaged over the last window.</summary>
        public int Fps { get; private set; }

        /// <summary>Gets the longest frame of the last window, in whole milliseconds.</summary>
        public int MaxFrameMs { get; private set; }

        /// <summary>Gets the most GC allocations made in one frame of the last window.</summary>
        public int MaxGcAllocations { get; private set; }

        /// <summary>Gets the number of active pieces, or 0 before <see cref="Initialize"/>.</summary>
        public int PieceCount => _pieces != null ? _pieces.ActivePieces.Count : 0;

        /// <summary>Gets the tier name shown, empty before <see cref="Initialize"/>.</summary>
        public string TierName => _tierName;

        /// <summary>
        /// Starts counting GC allocations per frame with the Profiler counter (<c>GC.GetAllocatedBytes</c> reads 0 in the Editor).
        /// </summary>
        private void OnEnable()
        {
            _gcAllocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
        }

        /// <summary>
        /// Releases the Profiler counter.
        /// </summary>
        private void OnDisable()
        {
            _gcAllocations.Dispose();
        }

        /// <summary>
        /// Samples the frame that just ended.
        /// </summary>
        private void Update()
        {
            Sample(Time.unscaledDeltaTime, _gcAllocations.Valid ? _gcAllocations.LastValue : 0L);
        }

        /// <summary>
        /// Draws the five rows in the top left corner, below the safe area notch.
        /// </summary>
        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 45) };
                _style.normal.textColor = Color.yellow;
            }

            var size = _style.fontSize;
            _left = Screen.safeArea.xMin + size;
            _top = Screen.height - Screen.safeArea.yMax + size;
            _rowHeight = size * ROW_HEIGHT_FACTOR;
            _labelWidth = size * LABEL_WIDTH_FACTOR;
            _valueWidth = size * VALUE_WIDTH_FACTOR;
            _row = 0;

            DrawRow("FPS", Number(Fps));
            DrawRow("Frame ms (max)", Number(MaxFrameMs));
            DrawRow("GC allocs (max)", Number(MaxGcAllocations));
            DrawRow("Pieces", Number(PieceCount));
            DrawRow("Tier", _tierName);
        }

        /// <summary>
        /// Gives the overlay what it reads. Both may be null (a scene without Boot or without pieces).
        /// </summary>
        /// <param name="pieces">The factory whose active pieces are counted, or null.</param>
        /// <param name="quality">The device tier shown, or null for none.</param>
        public void Initialize(PieceFactory pieces, IQualityTier quality)
        {
            _pieces = pieces;
            _tierName = quality != null ? quality.Level.ToString() : "n/a";
        }

        /// <summary>
        /// Records one frame. Public so a test can drive the windows without waiting for real time.
        /// </summary>
        /// <param name="deltaSeconds">The unscaled duration of the frame.</param>
        /// <param name="gcAllocations">The GC allocations counted in the frame.</param>
        public void Sample(float deltaSeconds, long gcAllocations)
        {
            _elapsed += deltaSeconds;
            _frames++;
            _maxDelta = Mathf.Max(_maxDelta, deltaSeconds);
            _maxAllocations = System.Math.Max(_maxAllocations, gcAllocations);

            if (_elapsed < WINDOW_SECONDS)
            {
                return;
            }

            Fps = Mathf.RoundToInt(_frames / _elapsed);
            MaxFrameMs = Mathf.RoundToInt(_maxDelta * 1000f);
            MaxGcAllocations = (int)System.Math.Min(_maxAllocations, int.MaxValue);
            _elapsed = 0f;
            _frames = 0;
            _maxDelta = 0f;
            _maxAllocations = 0;
        }

        /// <summary>
        /// Draws one label and its value on the next row of the current pass.
        /// </summary>
        /// <param name="label">The row name.</param>
        /// <param name="value">The row value.</param>
        private void DrawRow(string label, string value)
        {
            var top = _top + _row * _rowHeight;
            GUI.Label(new Rect(_left, top, _labelWidth, _rowHeight), label, _style);
            GUI.Label(new Rect(_left + _labelWidth, top, _valueWidth, _rowHeight), value, _style);
            _row++;
        }

        /// <summary>
        /// Returns the text of a whole number from a cache, so repeated values never build a string again. Larger
        /// values are clamped to the last cached number.
        /// </summary>
        /// <param name="value">The number to show.</param>
        /// <returns>The cached text.</returns>
        private string Number(int value)
        {
            var index = Mathf.Clamp(value, 0, MAX_CACHED_NUMBER);
            return _numbers[index] ?? (_numbers[index] = index.ToString());
        }
    }
}
#endif
