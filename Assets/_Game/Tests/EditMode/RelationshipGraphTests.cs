using System;
using NUnit.Framework;
using GosipSimulator.Gossip;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The opinion store without a scene, a bus or a save. Everything the blacksmith case rests on is
    /// here: that opinions are directed, that they clamp, and that a neutral interaction leaves no
    /// trace, because a trace is a row in the save and a dirty flag for nothing.
    /// </summary>
    public class RelationshipGraphTests
    {
        private const int MIN = -100;
        private const int MAX = 100;

        private const string BLACKSMITH = "blacksmith";
        private const string SON        = "son";
        private const string PLAYER     = "player";

        private static RelationshipGraph NewGraph()
        {
            return new RelationshipGraph(MIN, MAX);
        }

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Get_UnknownPair_ReturnsZero()
        {
            Assert.AreEqual(0, NewGraph().Get(BLACKSMITH, PLAYER));
        }

        [Test]
        public void Get_UnknownPair_StoresNothing()
        {
            var graph = NewGraph();

            graph.Get(BLACKSMITH, PLAYER);

            Assert.AreEqual(0, graph.Count, "Reading an opinion created one.");
        }

        [Test]
        public void Apply_AddsDeltaToAnUnknownPair()
        {
            var graph = NewGraph();

            Assert.AreEqual(-30, graph.Apply(BLACKSMITH, PLAYER, -30));
            Assert.AreEqual(-30, graph.Get(BLACKSMITH, PLAYER));
            Assert.AreEqual(1, graph.Count);
        }

        [Test]
        public void Apply_AccumulatesAcrossCalls()
        {
            var graph = NewGraph();

            graph.Apply(BLACKSMITH, PLAYER, -30);
            graph.Apply(BLACKSMITH, PLAYER, -5);
            int afterGoodTurn = graph.Apply(BLACKSMITH, PLAYER, 10);

            Assert.AreEqual(-25, afterGoodTurn, "A later action did not move the same opinion.");
            Assert.AreEqual(1, graph.Count, "Three changes to one pair stored three entries.");
        }

        [TestCase(MAX + 50)]
        [TestCase(int.MaxValue)]
        public void Apply_ClampsAtTheMaximum(int delta)
        {
            var graph = NewGraph();
            graph.Apply(BLACKSMITH, PLAYER, 40);

            Assert.AreEqual(MAX, graph.Apply(BLACKSMITH, PLAYER, delta));
        }

        [TestCase(MIN - 50)]
        [TestCase(int.MinValue)]
        public void Apply_ClampsAtTheMinimum(int delta)
        {
            var graph = NewGraph();
            graph.Apply(BLACKSMITH, PLAYER, -40);

            Assert.AreEqual(MIN, graph.Apply(BLACKSMITH, PLAYER, delta));
        }

        [Test]
        public void Apply_ZeroDeltaOnAnUnknownPair_StoresNothing()
        {
            var graph = NewGraph();

            Assert.AreEqual(0, graph.Apply(BLACKSMITH, PLAYER, 0));
            Assert.AreEqual(0, graph.Count,
                "A neutral interaction created a row. Every NPC the player walks past would end up in the save.");
        }

        [Test]
        public void Apply_ZeroDeltaOnAKnownPair_KeepsValueAndCount()
        {
            var graph = NewGraph();
            graph.Apply(BLACKSMITH, PLAYER, -30);

            Assert.AreEqual(-30, graph.Apply(BLACKSMITH, PLAYER, 0));
            Assert.AreEqual(1, graph.Count);
        }

        [Test]
        public void Apply_ExtremeDelta_DoesNotWrapAround()
        {
            var graph = NewGraph();
            graph.Apply(BLACKSMITH, PLAYER, 90);

            // Narrow arithmetic would overflow to a large negative and clamp to MIN, which reads as
            // the blacksmith suddenly adoring nobody. Widening before the add is what prevents it.
            Assert.AreEqual(MAX, graph.Apply(BLACKSMITH, PLAYER, int.MaxValue));
        }

        [Test]
        public void OpinionsAreDirected_OneEdgeDoesNotImplyTheReverse()
        {
            var graph = NewGraph();

            graph.Apply(BLACKSMITH, PLAYER, -80);

            Assert.AreEqual(-80, graph.Get(BLACKSMITH, PLAYER));
            Assert.AreEqual(0, graph.Get(PLAYER, BLACKSMITH),
                "The opinion ran backwards. A rumor from the son to the father would also move the father to the son.");
        }

        [Test]
        public void Set_OverwritesAnExistingOpinion()
        {
            var graph = NewGraph();
            graph.Apply(BLACKSMITH, PLAYER, -30);

            graph.Set(BLACKSMITH, PLAYER, 15);

            Assert.AreEqual(15, graph.Get(BLACKSMITH, PLAYER), "Set added to the value instead of replacing it.");
            Assert.AreEqual(1, graph.Count);
        }

        [TestCase(MAX + 1)]
        [TestCase(MIN - 1)]
        public void Set_ClampsAnOutOfRangeValue(int value)
        {
            var graph = NewGraph();

            graph.Set(BLACKSMITH, PLAYER, value);

            Assert.AreEqual(value > 0 ? MAX : MIN, graph.Get(BLACKSMITH, PLAYER),
                "A value from a hand-edited save was stored outside the configured range.");
        }

        [Test]
        public void Set_Zero_DropsTheEntry()
        {
            var graph = NewGraph();
            graph.Apply(BLACKSMITH, PLAYER, -30);

            graph.Set(BLACKSMITH, PLAYER, 0);

            Assert.AreEqual(0, graph.Get(BLACKSMITH, PLAYER));
            Assert.AreEqual(0, graph.Count,
                "Setting an opinion back to neutral kept a zero row. Set and Apply now disagree about what Count means.");
        }

        [Test]
        public void All_ExposesOnlyTheStoredOpinions()
        {
            var graph = NewGraph();
            graph.Apply(BLACKSMITH, PLAYER, -30);
            graph.Apply(SON, PLAYER, -10);
            graph.Get(BLACKSMITH, SON);

            Assert.AreEqual(2, graph.All.Count);
            Assert.AreEqual(-30, graph.All[(BLACKSMITH, PLAYER)]);
            Assert.AreEqual(-10, graph.All[(SON, PLAYER)]);
        }

        [TestCase(0, 0)]
        [TestCase(5, 5)]
        [TestCase(10, -10)]
        public void Constructor_InvertedOrEmptyRange_Throws(int minValue, int maxValue)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelationshipGraph(minValue, maxValue));
        }

        [TestCase(10, 100)]
        [TestCase(-100, -10)]
        public void Constructor_RangeThatExcludesZero_Throws(int minValue, int maxValue)
        {
            // An unknown pair reads as zero and the first delta lands on top of that reading, so a
            // range without zero in it would clamp every opinion the moment it was created.
            Assert.Throws<ArgumentOutOfRangeException>(() => new RelationshipGraph(minValue, maxValue));
        }

        [TestCase(null, PLAYER)]
        [TestCase("", PLAYER)]
        [TestCase("   ", PLAYER)]
        [TestCase(BLACKSMITH, null)]
        [TestCase(BLACKSMITH, "")]
        public void Get_BlankId_Throws(string npcId, string aboutId)
        {
            Assert.Throws<ArgumentException>(() => NewGraph().Get(npcId, aboutId));
        }

        [TestCase(BLACKSMITH, BLACKSMITH)]
        [TestCase(PLAYER, PLAYER)]
        public void Apply_SameIdOnBothSides_Throws(string npcId, string aboutId)
        {
            Assert.Throws<ArgumentException>(() => NewGraph().Apply(npcId, aboutId, -10));
        }

        #endregion
    }
}
