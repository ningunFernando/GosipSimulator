using System;
using System.Collections.Generic;
using NUnit.Framework;
using GosipSimulator.Core;
using GosipSimulator.Save;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The rules for what an opinion looks like on disk. The store is a copy of what Gossip owns,
    /// kept in step through OnRelationshipChanged because Save cannot reference Gossip (R3), so the
    /// two have to agree on the same sparseness: a row exists only while the opinion is non zero.
    /// </summary>
    public class RelationshipStoreTests
    {
        private const string Npc   = "blacksmith";
        private const string About = "player";

        private SaveData          _data;
        private RelationshipStore _store;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            _data  = new SaveData();
            _store = new RelationshipStore(_data);
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void NullData_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RelationshipStore(null));
        }

        [Test]
        public void NullList_IsRepairedOnConstruction()
        {
            // A hand built SaveData, or one JsonUtility filled from a file that never mentioned the
            // field, can arrive with no list at all.
            var data = new SaveData { relationships = null };

            var store = new RelationshipStore(data);

            Assert.IsNotNull(data.relationships, "The store left the save holding a null list.");
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void UnknownPair_ReadsAsZero()
        {
            // Matches RelationshipGraph: nobody has an opinion until something moves it.
            Assert.AreEqual(0, _store.Get(Npc, About));
        }

        [Test]
        public void Record_AddsOneRow()
        {
            Assert.IsTrue(_store.Record(Npc, About, -5, "robbery"));

            Assert.AreEqual(1, _store.Count);
            Assert.AreEqual(-5, _store.Get(Npc, About));
            Assert.AreEqual("robbery", _data.relationships[0].reason);
        }

        [Test]
        public void Record_SamePairTwice_UpdatesInPlace()
        {
            _store.Record(Npc, About, -5, "robbery");
            _store.Record(Npc, About, -12, "rumor");

            Assert.AreEqual(1, _store.Count, "The second change added a row instead of updating one.");
            Assert.AreEqual(-12, _store.Get(Npc, About));
            Assert.AreEqual("rumor", _data.relationships[0].reason, "The reason did not follow the value.");
        }

        [Test]
        public void Record_DifferentPairs_AreKeptApart()
        {
            _store.Record("son", About, -10, "robbery");
            _store.Record("blacksmith", About, -5, "robbery");
            _store.Record("son", "blacksmith", 20, "help");

            Assert.AreEqual(3, _store.Count);
            Assert.AreEqual(-10, _store.Get("son", About));
            Assert.AreEqual(-5, _store.Get("blacksmith", About));
            Assert.AreEqual(20, _store.Get("son", "blacksmith"));
        }

        [Test]
        public void Record_Direction_Matters()
        {
            // An opinion is directed. The son thinking badly of the player says nothing about what
            // the player thinks of the son, and the store must not conflate the two.
            _store.Record("son", "blacksmith", 20, "help");

            Assert.AreEqual(20, _store.Get("son", "blacksmith"));
            Assert.AreEqual(0, _store.Get("blacksmith", "son"), "The store read a reversed pair as the same row.");
        }

        [Test]
        public void Record_Zero_WritesNoRow()
        {
            Assert.IsTrue(_store.Record(Npc, About, 0, "robbery"));

            // Sparse on purpose: a zero row is indistinguishable from no row, and keeping it would
            // grow the file with entries that say nothing.
            Assert.AreEqual(0, _store.Count, "A neutral opinion was persisted.");
        }

        [Test]
        public void Record_BackToZero_RemovesTheRow()
        {
            _store.Record(Npc, About, -5, "robbery");
            _store.Record(Npc, About, 0, "forgiven");

            Assert.AreEqual(0, _store.Count, "An opinion back at zero left its row behind.");
            Assert.AreEqual(0, _store.Get(Npc, About));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Record_EmptyNpcId_IsRejected(string npcId)
        {
            Assert.IsFalse(_store.Record(npcId, About, -5, "robbery"));
            Assert.AreEqual(0, _store.Count);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Record_EmptyAboutId_IsRejected(string aboutId)
        {
            Assert.IsFalse(_store.Record(Npc, aboutId, -5, "robbery"));
            Assert.AreEqual(0, _store.Count);
        }

        [Test]
        public void Record_SelfOpinion_IsRejected()
        {
            // RelationshipGraph refuses this pair, so a row like it could never be loaded back.
            // Returning false rather than throwing keeps one bad event from taking the game down (R9).
            Assert.IsFalse(_store.Record(Npc, Npc, -5, "robbery"));
            Assert.AreEqual(0, _store.Count);
        }

        [Test]
        public void ToRestoredPayload_Empty_GivesEmptyArraysNotNull()
        {
            OnRelationshipsRestored payload = _store.ToRestoredPayload();

            // A fresh save is a valid answer. Gossip.Restore treats null as empty too, but handing
            // it real arrays keeps "no opinions" and "no payload" from looking the same.
            Assert.IsNotNull(payload.npcIds);
            Assert.IsNotNull(payload.aboutIds);
            Assert.IsNotNull(payload.values);
            Assert.AreEqual(0, payload.npcIds.Length);
        }

        [Test]
        public void ToRestoredPayload_CarriesEveryRowInOrder()
        {
            _store.Record("son", About, -10, "robbery");
            _store.Record("blacksmith", About, -5, "robbery");

            OnRelationshipsRestored payload = _store.ToRestoredPayload();

            Assert.AreEqual(2, payload.npcIds.Length);
            Assert.AreEqual(payload.npcIds.Length, payload.aboutIds.Length, "The three columns disagree.");
            Assert.AreEqual(payload.npcIds.Length, payload.values.Length, "The three columns disagree.");

            Assert.AreEqual("son", payload.npcIds[0]);
            Assert.AreEqual(About, payload.aboutIds[0]);
            Assert.AreEqual(-10, payload.values[0]);

            Assert.AreEqual("blacksmith", payload.npcIds[1]);
            Assert.AreEqual(-5, payload.values[1]);
        }

        [Test]
        public void ToRestoredPayload_SkipsNothingAfterAnUpdate()
        {
            _store.Record("son", About, -10, "robbery");
            _store.Record("blacksmith", About, -5, "robbery");
            _store.Record("son", About, -30, "robbery");

            OnRelationshipsRestored payload = _store.ToRestoredPayload();

            Assert.AreEqual(2, payload.npcIds.Length, "Updating a row changed how many there are.");

            var byNpc = new Dictionary<string, int>();

            for (int i = 0; i < payload.npcIds.Length; i++) byNpc[payload.npcIds[i]] = payload.values[i];

            Assert.AreEqual(-30, byNpc["son"], "The payload carried the value from before the update.");
            Assert.AreEqual(-5, byNpc["blacksmith"]);
        }

        [Test]
        public void Rows_SurviveAJsonRoundTrip()
        {
            // JsonUtility is what actually writes this, and it only serializes public fields on a
            // [Serializable] type. A private field or a property on RelationshipRow would read back
            // as a default without any warning, which is the failure this test exists to catch.
            _store.Record("son", About, -10, "robbery");
            _store.Record("blacksmith", About, -5, "rumor");

            string json = UnityEngine.JsonUtility.ToJson(_data);
            SaveData restored = UnityEngine.JsonUtility.FromJson<SaveData>(json);

            Assert.AreEqual(SaveData.CURRENT_VERSION, restored.saveVersion);
            Assert.AreEqual(2, restored.relationships.Count, "The rows did not survive serialisation.");

            var store = new RelationshipStore(restored);

            Assert.AreEqual(-10, store.Get("son", About));
            Assert.AreEqual(-5, store.Get("blacksmith", About));
            Assert.AreEqual("rumor", restored.relationships[1].reason, "The reason did not survive.");
        }

        #endregion
    }
}
