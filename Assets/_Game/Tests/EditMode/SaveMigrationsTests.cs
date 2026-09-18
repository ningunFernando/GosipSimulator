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
        public void Migrate_OldVersion_UpgradesWithoutDataLoss()
        {
            // CURRENT_VERSION is 1 and the shipped chain is empty, so the while loop in Migrate
            // has no production input yet and would stay dead code. The synthetic step below is
            // the only way to exercise the walk before the first real migration exists; TearDown
            // removes it again. When CURRENT_VERSION reaches 2, replace this with the real step.
            MigrationTable()[0] = data =>
            {
                data.saveVersion = 1;
                return data;
            };

            SaveData stored = new SaveData { saveVersion = 0, currency = 120, totalEarned = 300 };

            SaveData migrated = new SaveMigrations().Migrate(stored);

            Assert.IsNotNull(migrated, "A migration path existed and Migrate still gave up.");
            Assert.AreEqual(SaveData.CURRENT_VERSION, migrated.saveVersion);
            Assert.AreEqual(120, migrated.currency, "The migration lost the player's currency.");
            Assert.AreEqual(300, migrated.totalEarned, "The migration lost the lifetime accumulator.");
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
