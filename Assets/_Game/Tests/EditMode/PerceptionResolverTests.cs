using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GosipSimulator.Npcs;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The rule for who saw something. It decides how much being unobserved is worth, so the edges
    /// matter: standing exactly on the edge of somebody's sight, an NPC with no eyes, and the actor
    /// themselves, who is always standing where the action happened.
    /// </summary>
    public class PerceptionResolverTests
    {
        private const string Actor = "player";

        private PerceptionResolver _resolver;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            _resolver = new PerceptionResolver();
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void NullCandidates_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _resolver.Resolve(Vector3.zero, Actor, null));
        }

        [Test]
        public void NobodyAround_SeesNothing()
        {
            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, Actor, new List<PerceptionResolver.Candidate>()).Count);
        }

        [Test]
        public void SomebodyInRange_Sees()
        {
            var candidates = new List<PerceptionResolver.Candidate> { At("son", new Vector3(3f, 0f, 0f), 6f) };

            IReadOnlyList<string> witnesses = _resolver.Resolve(Vector3.zero, Actor, candidates);

            Assert.AreEqual(1, witnesses.Count);
            Assert.AreEqual("son", witnesses[0]);
        }

        [Test]
        public void SomebodyOutOfRange_SeesNothing()
        {
            var candidates = new List<PerceptionResolver.Candidate> { At("elder", new Vector3(20f, 0f, 0f), 6f) };

            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, Actor, candidates).Count);
        }

        [Test]
        public void ExactlyOnTheEdge_Sees()
        {
            // Inclusive on purpose. An exclusive edge would make a theft at exactly six units a
            // coin flip on floating point, which is not a rule anybody can reason about.
            var candidates = new List<PerceptionResolver.Candidate> { At("son", new Vector3(6f, 0f, 0f), 6f) };

            Assert.AreEqual(1, _resolver.Resolve(Vector3.zero, Actor, candidates).Count);
        }

        [Test]
        public void DistanceIsMeasuredInThreeDimensions()
        {
            // 3-4-5: five units away, inside a range of six, even though neither axis alone is.
            var candidates = new List<PerceptionResolver.Candidate> { At("son", new Vector3(3f, 0f, 4f), 6f) };

            Assert.AreEqual(1, _resolver.Resolve(Vector3.zero, Actor, candidates).Count);
        }

        [Test]
        public void HeightCounts()
        {
            // Somebody on a balcony straight above is eight units away, not zero.
            var candidates = new List<PerceptionResolver.Candidate> { At("son", new Vector3(0f, 8f, 0f), 6f) };

            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, Actor, candidates).Count);
        }

        [Test]
        public void TheActor_NeverWitnessesThemselves()
        {
            // The actor stands where the action happened, so they are always inside their own range.
            // RelationshipGraph rejects a self opinion, so letting this through would mean Gossip
            // refusing an event that perception should never have sent.
            var candidates = new List<PerceptionResolver.Candidate> { At(Actor, Vector3.zero, 6f) };

            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, Actor, candidates).Count);
        }

        [Test]
        public void TheActor_IsExcludedButOthersAreNot()
        {
            var candidates = new List<PerceptionResolver.Candidate>
            {
                At(Actor, Vector3.zero, 6f),
                At("son", new Vector3(1f, 0f, 0f), 6f)
            };

            IReadOnlyList<string> witnesses = _resolver.Resolve(Vector3.zero, Actor, candidates);

            Assert.AreEqual(1, witnesses.Count);
            Assert.AreEqual("son", witnesses[0]);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void ARangeThatIsNotPositive_SeesNothing(float sightRange)
        {
            // Standing on top of the action and still blind. NaN is in here because it would fail
            // the distance test anyway, but silently, and a blind NPC is hard to explain.
            var candidates = new List<PerceptionResolver.Candidate> { At("son", Vector3.zero, sightRange) };

            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, Actor, candidates).Count);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ACandidateWithNoId_IsSkipped(string npcId)
        {
            var candidates = new List<PerceptionResolver.Candidate> { At(npcId, Vector3.zero, 6f) };

            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, Actor, candidates).Count);
        }

        [Test]
        public void EachNpcUsesTheirOwnRange()
        {
            // Same spot, different eyes. The near-sighted one misses what the sharp-eyed one sees.
            var candidates = new List<PerceptionResolver.Candidate>
            {
                At("sharp", new Vector3(9f, 0f, 0f), 12f),
                At("blind", new Vector3(9f, 0f, 0f), 3f)
            };

            IReadOnlyList<string> witnesses = _resolver.Resolve(Vector3.zero, Actor, candidates);

            Assert.AreEqual(1, witnesses.Count);
            Assert.AreEqual("sharp", witnesses[0]);
        }

        [Test]
        public void Witnesses_ComeBackNearestFirst()
        {
            // The order decides who forms an opinion first and therefore which rumor is queued
            // first, so it is part of the contract and not an accident of how the scene was authored.
            var candidates = new List<PerceptionResolver.Candidate>
            {
                At("far", new Vector3(5f, 0f, 0f), 10f),
                At("near", new Vector3(1f, 0f, 0f), 10f),
                At("middle", new Vector3(3f, 0f, 0f), 10f)
            };

            IReadOnlyList<string> witnesses = _resolver.Resolve(Vector3.zero, Actor, candidates);

            Assert.AreEqual(new[] { "near", "middle", "far" }, witnesses);
        }

        [Test]
        public void SameDistance_IsBrokenByIdSoTheOrderIsStable()
        {
            var candidates = new List<PerceptionResolver.Candidate>
            {
                At("zoe", new Vector3(2f, 0f, 0f), 10f),
                At("adam", new Vector3(-2f, 0f, 0f), 10f)
            };

            IReadOnlyList<string> witnesses = _resolver.Resolve(Vector3.zero, Actor, candidates);

            Assert.AreEqual(new[] { "adam", "zoe" }, witnesses);
        }

        [Test]
        public void TheActionPoint_IsWhatDistanceIsMeasuredFrom()
        {
            // Not the origin, and not the actor's id: perception answers about a place.
            var candidates = new List<PerceptionResolver.Candidate>
            {
                At("son", new Vector3(20f, 0f, 0f), 6f),
                At("elder", Vector3.zero, 6f)
            };

            IReadOnlyList<string> witnesses = _resolver.Resolve(new Vector3(22f, 0f, 0f), Actor, candidates);

            Assert.AreEqual(1, witnesses.Count);
            Assert.AreEqual("son", witnesses[0], "Distance was measured from the wrong point.");
        }

        [Test]
        public void ResolvingTwice_DoesNotCarryTheFirstAnswerOver()
        {
            // The resolver reuses its buffer between calls, so a leak would show up as the previous
            // witnesses appearing in a later answer.
            var first = new List<PerceptionResolver.Candidate> { At("son", Vector3.zero, 6f) };
            var second = new List<PerceptionResolver.Candidate> { At("elder", new Vector3(50f, 0f, 0f), 6f) };

            Assert.AreEqual(1, _resolver.Resolve(Vector3.zero, Actor, first).Count);
            Assert.AreEqual(0, _resolver.Resolve(Vector3.zero, Actor, second).Count, "The buffer leaked between calls.");
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private static PerceptionResolver.Candidate At(string npcId, Vector3 position, float sightRange) =>
            new PerceptionResolver.Candidate
            {
                npcId      = npcId,
                position   = position,
                sightRange = sightRange
            };

        #endregion
    }
}
