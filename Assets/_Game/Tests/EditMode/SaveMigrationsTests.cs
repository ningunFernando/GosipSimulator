using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GosipSimulator.Save;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Covers M7: the reference project versioned the save from day one and then, on any version
    /// mismatch, replaced it with a fresh one. Migrating is not the same as deleting (R14).
    /// </summary>
    public class SaveMigrationsTests
    {
        private static readonly Regex NoPath = new Regex(@"^\[SaveMigrations\].*No migration path");

        private List<int> _shippedVersions;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            _shippedVersions = new List<int>(MigrationTable().Keys);
        }

        [TearDown]
        public void TearDown()
        {
            // The table is static, so a synthetic step added by a test would otherwise leak into
            // every later test and into the next run of the Editor session.
            Dictionary<int, Func<SaveData, SaveData>> table = MigrationTable();

            foreach (int version in new List<int>(table.Keys))
            {
                if (!_shippedVersions.Contains(version)) table.Remove(version);
            }
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Migrate_V1Save_UpgradesToV2AndKeepsProgress()
        {
            // The real shipped step now, not a synthetic one: v1 is what every save written before
            // the village existed looks like.
            SaveData stored = new SaveData { saveVersion = 1, currency = 120, totalEarned = 300 };

            SaveData migrated = new SaveMigrations().Migrate(stored);

            Assert.IsNotNull(migrated, "A migration path existed and Migrate still gave up.");
            Assert.AreEqual(SaveData.CURRENT_VERSION, migrated.saveVersion);
            Assert.AreEqual(120, migrated.currency, "The migration lost the player's currency.");
            Assert.AreEqual(300, migrated.totalEarned, "The migration lost the lifetime accumulator.");

            Assert.IsNotNull(migrated.relationships, "v2 must always have a list, never null.");
            Assert.AreEqual(0, migrated.relationships.Count,
                "A village that was never played against has wronged nobody.");
        }

        [Test]
        public void Migrate_V1SaveWithNoRelationshipList_GetsAnEmptyOne()
        {
            // JsonUtility reading a v1 file leaves any field the file does not mention at whatever
            // the constructor set, but a SaveData built by hand, or a future field whose default
            // changes, can still arrive null. The migration repairs it instead of passing null on.
            SaveData stored = new SaveData { saveVersion = 1, relationships = null };

            SaveData migrated = new SaveMigrations().Migrate(stored);

            Assert.IsNotNull(migrated);
            Assert.IsNotNull(migrated.relationships, "The migration passed a null list forward.");
            Assert.AreEqual(0, migrated.relationships.Count);
        }

        [Test]
        public void Migrate_AcrossTwoVersions_WalksTheWholeChain()
        {
            // A save old enough to need more than one step. The v0 step is synthetic because no
            // real one exists, and TearDown removes it; the v1 step it hands over to is the
            // shipped one, so this covers the walk itself and not just a single hop.
            MigrationTable()[0] = data =>
            {
                data.saveVersion = 1;
                return data;
            };

            SaveData stored = new SaveData { saveVersion = 0, currency = 42, totalEarned = 77 };

            SaveData migrated = new SaveMigrations().Migrate(stored);

            Assert.IsNotNull(migrated, "The chain existed end to end and Migrate still gave up.");
            Assert.AreEqual(SaveData.CURRENT_VERSION, migrated.saveVersion);
            Assert.AreEqual(42, migrated.currency, "A two step walk lost the player's currency.");
            Assert.AreEqual(77, migrated.totalEarned);
            Assert.IsNotNull(migrated.relationships, "The v1 step did not run on the way through.");
        }

        [Test]
        public void Migrate_CurrentVersion_ReturnsDataUntouched()
        {
            SaveData stored = new SaveData { currency = 10, totalEarned = 10 };

            SaveData result = new SaveMigrations().Migrate(stored);

            Assert.AreSame(stored, result);
        }

        [Test]
        public void Migrate_VersionWithoutPath_ReturnsNullAndKeepsData()
        {
            SaveData stored = new SaveData { saveVersion = 0, currency = 999, totalEarned = 999 };

            LogAssert.Expect(LogType.Error, NoPath);
            SaveData result = new SaveMigrations().Migrate(stored);

            Assert.IsNull(result, "Migrate must report failure instead of inventing a save.");

            // The caller decides what to do. Nothing is wiped here, which is the whole of M7.
            Assert.AreEqual(999, stored.currency);
            Assert.AreEqual(999, stored.totalEarned);
        }

        [Test]
        public void Migrate_NewerThanCurrentVersion_ReturnsNull()
        {
            // A save written by a newer build. Downgrading is never attempted and the loop never
            // runs, so this exits through the final version check rather than through the chain.
            SaveData stored = new SaveData { saveVersion = SaveData.CURRENT_VERSION + 1 };

            Assert.IsNull(new SaveMigrations().Migrate(stored));
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        /// <summary>
        /// The chain is a private static dictionary. Reaching it by reflection keeps SaveMigrations
        /// free of a constructor that exists only for tests; the readonly field holds a mutable
        /// dictionary, so entries can be added and removed around a test.
        /// </summary>
        private static Dictionary<int, Func<SaveData, SaveData>> MigrationTable()
        {
            FieldInfo field = typeof(SaveMigrations)
                .GetField("Migrations", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(field,
                "SaveMigrations.Migrations was renamed or removed. Update this test seam.");

            return (Dictionary<int, Func<SaveData, SaveData>>)field.GetValue(null);
        }

        #endregion
    }
}
