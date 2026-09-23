using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GosipSimulator.Tools;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Milestone 2 of the node tool: the village turned into something drawable. No window and no
    /// assets here, which is the whole point of keeping the rules out of the GraphView (R5).
    ///
    /// The cases that matter are the ones a person creates by hand in the Inspector: a tie to a name
    /// that was renamed, a tie left empty, the same target added twice. Each one is reported rather
    /// than drawn, and the report is what milestone 9 will paint on the node.
    /// </summary>
    public class VillageRulesTests
    {
        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void TheVillageAsItStands_DrawsOneEdgePerTie()
        {
            VillageGraphData data = VillageRules.Build(TheVillage());

            Assert.AreEqual(3, data.Edges.Count, "The village has three ties today.");
            Assert.IsEmpty(data.Problems, "Nothing in the current assets is broken.");

            AssertEdge(data.Edges[0], "son", "blacksmith", 90);
            AssertEdge(data.Edges[1], "blacksmith", "villager", 50);
            AssertEdge(data.Edges[2], "villager", "elder", 40);
        }

        [Test]
        public void ATieIsDrawn_EvenWhenItPointsAtAnNpcDeclaredLater()
        {
            // The son is read before the blacksmith exists as far as the loop is concerned. A single
            // pass would call this a tie to nobody.
            var npcs = new List<NpcSnapshot>
            {
                Npc("son", ("blacksmith", 90)),
                Npc("blacksmith")
            };

            VillageGraphData data = VillageRules.Build(npcs);

            Assert.AreEqual(1, data.Edges.Count);
            Assert.IsEmpty(data.Problems);
        }

        [Test]
        public void ATieToSomebodyWhoDoesNotExist_IsReportedAndNotDrawn()
        {
            VillageGraphData data = VillageRules.Build(new List<NpcSnapshot> { Npc("son", ("blacksmyth", 90)) });

            Assert.IsEmpty(data.Edges, "There is no node to connect to.");
            Assert.AreEqual(1, data.Problems.Count);
            Assert.AreEqual("son", data.Problems[0].NpcId, "The NPC holding the tie is the one to blame.");
            StringAssert.Contains("blacksmyth", data.Problems[0].Message);
        }

        [Test]
        public void ATieWithNobodyOnTheOtherEnd_IsReportedAndNotDrawn()
        {
            VillageGraphData data = VillageRules.Build(new List<NpcSnapshot> { Npc("son", ("", 90)) });

            Assert.IsEmpty(data.Edges);
            Assert.AreEqual(1, data.Problems.Count);
        }

        [Test]
        public void ATieToThemselves_IsReportedAndNotDrawn()
        {
            VillageGraphData data = VillageRules.Build(new List<NpcSnapshot> { Npc("son", ("son", 90)) });

            Assert.IsEmpty(data.Edges);
            Assert.AreEqual(1, data.Problems.Count);
            StringAssert.Contains("themselves", data.Problems[0].Message);
        }

        [Test]
        public void TwoTiesBetweenTheSamePair_DrawOnlyTheFirst()
        {
            var npcs = new List<NpcSnapshot>
            {
                Npc("son", ("blacksmith", 90), ("blacksmith", 10)),
                Npc("blacksmith")
            };

            VillageGraphData data = VillageRules.Build(npcs);

            Assert.AreEqual(1, data.Edges.Count);
            Assert.AreEqual(90, data.Edges[0].Trust, "The first tie is the one kept.");
            Assert.AreEqual(1, data.Problems.Count, "The hidden one is still worth saying out loud.");
        }

        [Test]
        public void AnNpcWithNoId_IsReportedAndTiesNothing()
        {
            var npcs = new List<NpcSnapshot>
            {
                Npc("", ("blacksmith", 90)),
                Npc("blacksmith")
            };

            VillageGraphData data = VillageRules.Build(npcs);

            Assert.IsEmpty(data.Edges, "A tie has to start somewhere.");
            Assert.AreEqual(1, data.Problems.Count);
            Assert.IsEmpty(data.Problems[0].NpcId, "Nobody can be named when the id is the missing part.");
        }

        [Test]
        public void TwoNpcsSharingAnId_AreReported()
        {
            VillageGraphData data = VillageRules.Build(new List<NpcSnapshot> { Npc("son"), Npc("son") });

            Assert.AreEqual(1, data.Problems.Count);
            Assert.AreEqual("son", data.Problems[0].NpcId);
        }

        [Test]
        public void AnEmptyVillage_IsNotAProblem()
        {
            VillageGraphData none = VillageRules.Build(null);

            Assert.IsEmpty(none.Npcs);
            Assert.IsEmpty(none.Edges);
            Assert.IsEmpty(none.Problems);

            Assert.IsEmpty(VillageRules.Build(new List<NpcSnapshot>()).Problems);
        }

        [Test]
        public void EveryNpcKeepsItsNode_EvenTheBrokenOnes()
        {
            // You cannot fix an id in a node that was never drawn.
            var npcs = new List<NpcSnapshot> { Npc(""), Npc("son"), Npc("son") };

            Assert.AreEqual(3, VillageRules.Build(npcs).Npcs.Count);
        }

        [Test]
        public void FallbackPositions_FillRowsAndNeverOverlap()
        {
            var seen = new HashSet<Vector2>();

            for (int i = 0; i < VillageRules.COLUMNS * 3; i++)
            {
                Assert.IsTrue(seen.Add(VillageRules.FallbackPosition(i)), $"Node {i} landed on top of another.");
            }

            Assert.AreEqual(Vector2.zero, VillageRules.FallbackPosition(0));
            Assert.AreEqual(VillageRules.SPACING.y, VillageRules.FallbackPosition(VillageRules.COLUMNS).y,
                "The row after the last column starts one row down.");
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        /// <summary>The four assets as they are in the project today, so a change there fails here.</summary>
        private static List<NpcSnapshot> TheVillage()
        {
            return new List<NpcSnapshot>
            {
                Npc("son",        ("blacksmith", 90)),
                Npc("blacksmith", ("villager",   50)),
                Npc("villager",   ("elder",      40)),
                Npc("elder")
            };
        }

        private static NpcSnapshot Npc(string id, params (string to, int trust)[] ties)
        {
            var list = new List<TieSnapshot>(ties.Length);

            foreach ((string to, int trust) tie in ties) list.Add(new TieSnapshot(tie.to, tie.trust));

            return new NpcSnapshot(id, id + " (display)", list);
        }

        private static void AssertEdge(VillageEdge edge, string fromId, string toId, int trust)
        {
            Assert.AreEqual(fromId, edge.FromId);
            Assert.AreEqual(toId,   edge.ToId);
            Assert.AreEqual(trust,  edge.Trust);
        }

        #endregion
    }
}