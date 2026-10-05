using System;
using Unity.Profiling;
using UnityEngine.Profiling;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Counts the managed allocations made by a piece of code, for the zero-allocation criteria. It uses the
    /// Profiler counter "GC Allocation In Frame Count", which is exact and updated at once. The usual .NET
    /// counters do not work in the Mono of Unity: <c>GC.GetAllocatedBytesForCurrentThread</c> always returns 0 and
    /// the heap size only moves when the heap grows.
    /// </summary>
    public static class AllocationMeter
    {
        /// <summary>
        /// Allocations that still count as "none". Other Editor threads can make a few while the code runs; code
        /// that allocates on every call makes at least as many as it has calls, far above this.
        /// </summary>
        public const long TOLERANCE_COUNT = 20;

        /// <summary>
        /// Runs an action with the Profiler on and returns how many managed allocations were made meanwhile.
        /// </summary>
        /// <param name="action">The code to measure. Build any delegate or data it needs before calling this.</param>
        /// <returns>Number of allocations.</returns>
        public static long Measure(Action action)
        {
            var wasEnabled = Profiler.enabled;
            Profiler.enabled = true;
            try
            {
                using (var counter = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count"))
                {
                    var before = counter.CurrentValue;
                    action();
                    return counter.CurrentValue - before;
                }
            }
            finally
            {
                Profiler.enabled = wasEnabled;
            }
        }

        /// <summary>
        /// Measures an action several times and returns the lowest count. The counter is global, so another Editor
        /// thread can add allocations to one attempt, but code that really allocates does so in every attempt. A first,
        /// discarded run absorbs the one-time cost of the Profiler and of the first call. Use it only for actions that
        /// can run repeatedly.
        /// </summary>
        /// <param name="action">The code to measure. It must give the same result when run again.</param>
        /// <param name="attempts">Number of times to measure.</param>
        /// <returns>The smallest number of allocations of any attempt.</returns>
        public static long MeasureLowest(Action action, int attempts = 5)
        {
            Measure(action);
            var lowest = long.MaxValue;
            for (var i = 0; i < attempts; i++)
            {
                lowest = Math.Min(lowest, Measure(action));
            }

            return lowest;
        }
    }
}
