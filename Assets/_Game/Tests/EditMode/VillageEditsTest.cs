using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GosipSimulator.Data;
using GosipSimulator.Tools;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Milestone 4 of the node tool: the first time it writes to an NPC asset. Everything here runs on
    /// an NpcDefinitionSO that lives only in memory, which SerializedObject edits exactly as it would
    /// edit the one on disk.
    ///
    /// The first test is the one that matters most over time. It fails the day a field in
    /// NpcDefinitionSO is renamed, which is otherwise the day the tool quietly stops writing.
    /// </summary>
    public class VillageEditsTests
    {
        private NpcDefinitionSO _son;

        // ────────────────────────────────
        // SETUP AND TEARDOWN
        // ────────────────────────────────
        #region Setup and Teardown

        [SetUp]
        public void SetUp()
        {
            _son = ScriptableObject.CreateInstance<NpcDefinitionSO>();
        }

        [TearDown]
        public void TearDown()
        {
            // The writes recorded Undo steps against this object. Cleared before it is destroyed, so the
            // Editor's history holds nothing that points at it.
            Undo.ClearUndo(_son);
            Object.DestroyImmediate(_son);
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void TheFieldNames_StillMatchNpcDefinitionSO()
        {
            var serialized = new SerializedObject(_son);
            SerializedProperty ties = serialized.FindProperty(VillageEdits.TIES);

            Assert.IsNotNull(ties, $"NpcDefinitionSO has no field '{VillageEdits.TIES}' any more.");

            // Grown but never applied, so the asset itself stays empty.
            ties.arraySize = 1;
            SerializedProperty tie = ties.GetArrayElementAtIndex(0);

            Assert.IsNotNull(tie.FindPropertyRelative(VillageEdits.OTHER_ID),
                $"SocialTie has no field '{VillageEdits.OTHER_ID}' any more.");
            Assert.IsNotNull(tie.FindPropertyRelative(VillageEdits.TRUST),
                $"SocialTie has no field '{VillageEdits.TRUST}' any more.");
        }

        [Test]
        public void AddingATie_WritesItWithItsTrust()
        {
            Assert.IsTrue(VillageEdits.AddTie(_son, "blacksmith", 90));

            Assert.AreEqual(1, _son.Ties.Count);
            Assert.AreEqual("blacksmith", _son.Ties[0].OtherNpcId);
            Assert.AreEqual(90, _son.Ties[0].Trust);
        }

        [Test]
        public void AddingATie_KeepsTheOnesAlreadyThere()
        {
            VillageEdits.AddTie(_son, "blacksmith", 90);
            VillageEdits.AddTie(_son, "villager",   20);

            Assert.AreEqual(2, _son.Ties.Count);
            Assert.AreEqual("blacksmith", _son.Ties[0].OtherNpcId, "A new tie goes at the end, so the old ones stay put.");
            Assert.AreEqual("villager",   _son.Ties[1].OtherNpcId);
            Assert.AreEqual(20, _son.Ties[1].Trust,
                "Growing the array copies the last tie. The new one must not inherit its trust.");
        }

        [Test]
        public void ATieWithNobodyOnEitherEnd_IsNotWritten()
        {
            Assert.IsFalse(VillageEdits.AddTie(_son, "",   VillageEdits.DEFAULT_TRUST));
            Assert.IsFalse(VillageEdits.AddTie(_son, null, VillageEdits.DEFAULT_TRUST));
            Assert.IsFalse(VillageEdits.AddTie(null, "blacksmith", VillageEdits.DEFAULT_TRUST));

            Assert.AreEqual(0, _son.Ties.Count);
        }

        [Test]
        public void ATrustGossipWouldReject_IsNotWritten()
        {
            Assert.IsFalse(VillageEdits.AddTie(_son, "blacksmith", VillageEdits.MIN_TRUST - 1));
            Assert.IsFalse(VillageEdits.AddTie(_son, "blacksmith", VillageEdits.MAX_TRUST + 1));

            Assert.AreEqual(0, _son.Ties.Count, "SocialGraph would throw, and the whole village would switch off.");
        }

        [Test]
        public void RemovingATie_LeavesTheOthers()
        {
            VillageEdits.AddTie(_son, "blacksmith", 90);
            VillageEdits.AddTie(_son, "villager",   20);

            Assert.IsTrue(VillageEdits.RemoveTie(_son, "blacksmith"));

            Assert.AreEqual(1, _son.Ties.Count);
            Assert.AreEqual("villager", _son.Ties[0].OtherNpcId);
        }

        [Test]
        public void RemovingATieThatIsNotThere_ChangesNothing()
        {
            VillageEdits.AddTie(_son, "blacksmith", 90);

            Assert.IsFalse(VillageEdits.RemoveTie(_son, "elder"));
            Assert.AreEqual(1, _son.Ties.Count);
        }

        [Test]
        public void RemovingOneOfTwoTiesToTheSameNpc_RemovesTheOneThatWasDrawn()
        {
            VillageEdits.AddTie(_son, "blacksmith", 90);
            VillageEdits.AddTie(_son, "blacksmith", 10);

            VillageEdits.RemoveTie(_son, "blacksmith");

            Assert.AreEqual(1, _son.Ties.Count);
            Assert.AreEqual(10, _son.Ties[0].Trust, "The first tie is the drawn one, so it is the one a delete removes.");
        }

        [Test]
        public void AnAddedTie_CanBeUndone()
        {
            // A group boundary first, so the undo below takes back this tie and nothing that came before.
            Undo.IncrementCurrentGroup();

            VillageEdits.AddTie(_son, "blacksmith", 90);
            Undo.PerformUndo();

            Assert.AreEqual(0, _son.Ties.Count, "The tie went through SerializedObject, so it should be in the history.");
        }

        #endregion
    }
}