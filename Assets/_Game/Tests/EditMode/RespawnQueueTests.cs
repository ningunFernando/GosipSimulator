using System;
using System.Collections.Generic;
using NUnit.Framework;
using GosipSimulator.Pickups;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Respawn timing without a scene. The zero deltaTime case is the paused game: PausedState sets
    /// timeScale to 0, and nothing may come back while it lasts.
    /// </summary>
    public class RespawnQueueTests
    {
        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Tick_BeforeDelay_ReleasesNothing()
        {
            var queue = new RespawnQueue();
            var ready = new List<int>();

            queue.Schedule(0, 2f);
            queue.Tick(1.5f, ready);

            Assert.IsEmpty(ready);
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Tick_PastDelay_ReleasesDuePointsInSchedulingOrder()
        {
            var queue = new RespawnQueue();
            var ready = new List<int>();

            queue.Schedule(3, 1f);
            queue.Schedule(1, 0.5f);
            queue.Schedule(2, 5f);
            queue.Tick(1.2f, ready);

            CollectionAssert.AreEqual(new[] { 3, 1 }, ready);
            Assert.AreEqual(1, queue.Count, "The point that is still waiting was dropped.");
        }

        [Test]
        public void Tick_ZeroDeltaTime_NeverReleasesAnything()
        {
            var queue = new RespawnQueue();
            var ready = new List<int>();

            queue.Schedule(0, 0f);
            queue.Tick(0f, ready);

            Assert.IsEmpty(ready, "A respawn came back while time was stopped.");

            queue.Tick(0.01f, ready);

            CollectionAssert.AreEqual(new[] { 0 }, ready);
        }

        [Test]
        public void Tick_SecondCall_ClearsPreviousReadyList()
        {
            var queue = new RespawnQueue();
            var ready = new List<int>();

            queue.Schedule(0, 0.1f);
            queue.Tick(0.2f, ready);
            queue.Tick(0.2f, ready);

            Assert.IsEmpty(ready, "A point was released twice.");
        }

        [TestCase(-1, 1f)]
        [TestCase(0,  -1f)]
        [TestCase(0,  float.NaN)]
        public void Schedule_InvalidArguments_Throws(int pointIndex, float delay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RespawnQueue().Schedule(pointIndex, delay));
        }

        #endregion
    }
}
