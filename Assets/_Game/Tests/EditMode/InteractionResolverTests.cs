using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GosipSimulator.Actions;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The reach rule: what the player is acting on when they press the key. The interesting cases
    /// are the edges, because reach is what stops a theft being possible from across the room, and
    /// the tie, because two things equally close has to have one answer rather than whichever the
    /// scene happened to list first.
    /// </summary>
    public class InteractionResolverTests
    {
        private InteractionResolver _resolver;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            _resolver = new InteractionResolver();
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void NullPositions_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(Vector3.zero, 2.5f, null));
        }

        [Test]
        public void NothingInTheWorld_ReachesNothing()
        {
            Assert.AreEqual(InteractionResolver.NONE, _resolver.Resolve(Vector3.zero, 2.5f, new List<Vector3>()));
        }

        [Test]
        public void SomethingInReach_IsChosen()
        {
            var positions = new List<Vector3> { new Vector3(1f, 0f, 0f) };

            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [Test]
        public void SomethingOutOfReach_IsNotChosen()
        {
            var positions = new List<Vector3> { new Vector3(10f, 0f, 0f) };

            Assert.AreEqual(InteractionResolver.NONE, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [Test]
        public void ExactlyAtTheLimit_IsChosen()
        {
            // Inclusive, like perception's edge. An exclusive limit makes an object at exactly the
            // reach distance a coin flip on floating point, which is not a rule anybody can play by.
            var positions = new List<Vector3> { new Vector3(2.5f, 0f, 0f) };

            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [Test]
        public void TheNearestWins()
        {
            var positions = new List<Vector3>
            {
                new Vector3(2f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(1f, 0f, 0f)
            };

            Assert.AreEqual(1, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [Test]
        public void TheNearestWins_EvenWhenItComesLast()
        {
            var positions = new List<Vector3>
            {
                new Vector3(2f, 0f, 0f),
                new Vector3(0.2f, 0f, 0f)
            };

            Assert.AreEqual(1, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [Test]
        public void ATie_KeepsTheEarlierIndex()
        {
            // Rare in play, certain in a test. The answer is fixed so the same standing spot always
            // acts on the same thing.
            var positions = new List<Vector3>
            {
                new Vector3(1f, 0f, 0f),
                new Vector3(-1f, 0f, 0f)
            };

            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [Test]
        public void OutOfReachOnesAreSkipped_NotCounted()
        {
            // The index returned has to point at the right entry even when nearer entries in the
            // list were rejected, which is what an off by one here would break.
            var positions = new List<Vector3>
            {
                new Vector3(50f, 0f, 0f),
                new Vector3(60f, 0f, 0f),
                new Vector3(1f, 0f, 0f)
            };

            Assert.AreEqual(2, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [Test]
        public void ReachIsMeasuredInThreeDimensions()
        {
            // 3-4-5 again: five away, outside a reach of 2.5 even though no single axis is.
            var positions = new List<Vector3> { new Vector3(3f, 0f, 4f) };

            Assert.AreEqual(InteractionResolver.NONE, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [Test]
        public void HeightCounts()
        {
            var positions = new List<Vector3> { new Vector3(0f, 5f, 0f) };

            Assert.AreEqual(InteractionResolver.NONE, _resolver.Resolve(Vector3.zero, 2.5f, positions));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void AReachThatIsNotPositive_ReachesNothing(float reach)
        {
            // Standing on top of it and still unable to act. Zero is a valid way to switch
            // interaction off; NaN would fail every comparison silently, so it is rejected up front.
            var positions = new List<Vector3> { Vector3.zero };

            Assert.AreEqual(InteractionResolver.NONE, _resolver.Resolve(Vector3.zero, reach, positions));
        }

        [Test]
        public void ReachIsMeasuredFromWhereThePlayerStands()
        {
            // Not from the origin. The same object is out of reach from one spot and in reach from
            // another, which is the whole point of walking up to something.
            var positions = new List<Vector3> { new Vector3(10f, 0f, 0f) };

            Assert.AreEqual(InteractionResolver.NONE, _resolver.Resolve(Vector3.zero, 2.5f, positions));
            Assert.AreEqual(0, _resolver.Resolve(new Vector3(9f, 0f, 0f), 2.5f, positions));
        }

        #endregion
    }
}
