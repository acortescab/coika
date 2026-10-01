using System;
using Coika.Data;
using Coika.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks that a <see cref="SpawnQueue"/> can be saved and resumed with <see cref="SpawnQueueState"/> and
    /// continues the same sequence (issue #5): the groundwork for resuming a run and for replays.
    /// </summary>
    public class SpawnQueueStateTests
    {
        private const int SEED = TestSpawnSettings.SEED;

        /// <summary>
        /// Advances a queue a number of times.
        /// </summary>
        /// <param name="queue">The queue to advance.</param>
        /// <param name="count">Number of advances.</param>
        private static void AdvanceTimes(SpawnQueue queue, int count)
        {
            for (int i = 0; i < count; i++)
            {
                queue.Advance();
            }
        }

        /// <summary>
        /// Asserts that two queues have the same Current and Next now and keep producing the same pieces.
        /// </summary>
        /// <param name="expected">The queue that was never saved.</param>
        /// <param name="restored">The queue that was resumed.</param>
        /// <param name="count">Number of further pieces to compare.</param>
        /// <param name="message">Text shown when they differ.</param>
        private static void AssertSameFromNowOn(SpawnQueue expected, SpawnQueue restored, int count, string message)
        {
            Assert.AreEqual(expected.Current, restored.Current, message + " (Current)");
            Assert.AreEqual(expected.Next, restored.Next, message + " (Next)");
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(expected.Advance(), restored.Advance(), $"{message} (piece {i})");
            }
        }

        /// <summary>
        /// A new queue reports its seed, no advances and the current layout version.
        /// </summary>
        [Test]
        public void GetState_OnANewQueue_HoldsTheSeedZeroAdvancesAndTheCurrentVersion()
        {
            var state = new SpawnQueue(TestSpawnSettings.Default(), SEED).GetState();

            Assert.AreEqual(SEED, state.Seed);
            Assert.AreEqual(0, state.AdvanceCount);
            Assert.AreEqual(SpawnQueueState.CURRENT_VERSION, state.Version);
        }

        /// <summary>
        /// The state counts the advances.
        /// </summary>
        [Test]
        public void GetState_AfterAdvancing_CountsTheAdvances()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);
            AdvanceTimes(queue, 7);

            Assert.AreEqual(7, queue.GetState().AdvanceCount);
        }

        /// <summary>
        /// Saving a queue in the middle of a run and restoring the state in another queue continues the same
        /// sequence, including when that queue started with another seed.
        /// </summary>
        [Test]
        public void SetState_AfterAdvancing_ContinuesTheSameSequence()
        {
            var original = new SpawnQueue(TestSpawnSettings.Default(), SEED);
            AdvanceTimes(original, 50);
            var restored = new SpawnQueue(TestSpawnSettings.Default(), 999);

            restored.SetState(original.GetState());

            Assert.AreEqual(SEED, restored.Seed, "The seed comes from the state.");
            AssertSameFromNowOn(original, restored, 200, "restored");
        }

        /// <summary>
        /// The state survives being written as JSON and read back, as it will in the save file.
        /// </summary>
        [Test]
        public void SetState_ThroughJsonUtility_ContinuesTheSameSequence()
        {
            var original = new SpawnQueue(TestSpawnSettings.Default(), SEED);
            AdvanceTimes(original, 123);
            var json = JsonUtility.ToJson(original.GetState());
            var restored = new SpawnQueue(TestSpawnSettings.Default(), 1);

            restored.SetState(JsonUtility.FromJson<SpawnQueueState>(json));

            AssertSameFromNowOn(original, restored, 200, "restored from json");
        }

        /// <summary>
        /// Resuming works at every point of a run, also inside the forced opening and with a streak in progress,
        /// because the replay rebuilds all of it.
        /// </summary>
        [Test]
        public void SetState_AtEveryPointOfARun_ContinuesLikeTheOriginal()
        {
            var settings = new SpawnSettings(new[] { 100f, 1f }, 2, 2, new[] { 0, 0 });

            for (int advances = 0; advances <= 40; advances++)
            {
                var original = new SpawnQueue(settings, SEED);
                AdvanceTimes(original, advances);
                var restored = new SpawnQueue(settings, 7);

                restored.SetState(original.GetState());

                AssertSameFromNowOn(original, restored, 30, $"after {advances} advances");
            }
        }

        /// <summary>
        /// Restoring raises Advanced once, so a preview refreshes.
        /// </summary>
        [Test]
        public void SetState_Always_RaisesAdvancedOnce()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);
            var calls = 0;
            queue.Advanced += () => calls++;

            queue.SetState(new SpawnQueue(TestSpawnSettings.Default(), SEED).GetState());

            Assert.AreEqual(1, calls);
        }

        /// <summary>
        /// A state with an unknown version is rejected, and the queue is left as it was.
        /// </summary>
        [Test]
        public void SetState_WithAnUnknownVersion_ThrowsAndLeavesTheQueueUnchanged()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);
            AdvanceTimes(queue, 5);
            var current = queue.Current;

            Assert.Throws<ArgumentException>(() => queue.SetState(new SpawnQueueState { Version = 99, Seed = 1, AdvanceCount = 0 }));

            Assert.AreEqual(SEED, queue.Seed);
            Assert.AreEqual(current, queue.Current);
        }

        /// <summary>
        /// A negative advance count, one above the limit (a corrupt save that would freeze the replay) and a
        /// missing state are rejected.
        /// </summary>
        [Test]
        public void SetState_WithABadState_Throws()
        {
            var queue = new SpawnQueue(TestSpawnSettings.Default(), SEED);

            Assert.Throws<ArgumentOutOfRangeException>(() => queue.SetState(new SpawnQueueState { Seed = 1, AdvanceCount = -1 }), "negative count");
            Assert.Throws<ArgumentOutOfRangeException>(() => queue.SetState(new SpawnQueueState { Seed = 1, AdvanceCount = SpawnQueue.MAX_ADVANCE_COUNT + 1 }), "count above the limit");
            Assert.Throws<ArgumentNullException>(() => queue.SetState(null), "null state");
        }
    }
}
