using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GosipSimulator.Save;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Covers C6 and R14. Runs against a temporary folder because JsonSaveStorage takes its path
    /// as a constructor argument and never touches Application.persistentDataPath: that injection
    /// is what makes the storage layer testable at all (R5).
    /// </summary>
    public class JsonSaveStorageTests
    {
        private static readonly Regex CorruptFile = new Regex(@"^\[JsonSaveStorage\].*corrupt");

        private string _folder;
        private string _filePath;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "GosipSimulatorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);

            _filePath = Path.Combine(_folder, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Load_WhenFileMissing_ReturnsNull()
        {
            // Null means "no usable save yet", which SaveSystem turns into a fresh SaveData.
            Assert.IsNull(NewStorage().Load());
        }

        [Test]
        public void Save_ThenLoad_RoundTripsEveryField()
        {
            ISaveStorage storage = NewStorage();

            storage.Save(new SaveData { currency = 250, totalEarned = 900 });

            SaveData loaded = storage.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveData.CURRENT_VERSION, loaded.saveVersion);
            Assert.AreEqual(250, loaded.currency);
            Assert.AreEqual(900, loaded.totalEarned);
        }

        [Test]
        public void Save_LeavesNoTemporaryFileBehind()
        {
            NewStorage().Save(new SaveData());

            // The .tmp is the transaction. A leftover one means the move failed and the next Load
            // would read a file nobody finished writing (C6).
            Assert.IsFalse(File.Exists(_filePath + ".tmp"), "The transactional write left its .tmp behind.");
            Assert.IsTrue(File.Exists(_filePath));
        }

        [Test]
        public void Save_OverExistingFile_ReplacesIt()
        {
            ISaveStorage storage = NewStorage();

            storage.Save(new SaveData { currency = 1 });
            storage.Save(new SaveData { currency = 2 });

            // The three-argument File.Move does not exist at this project's API compatibility
            // level, so the destination is deleted before the move. This is that path.
            Assert.AreEqual(2, storage.Load().currency);
        }

        [Test]
        public void Backup_WritesVersionedCopyAndKeepsOriginal()
        {
            ISaveStorage storage = NewStorage();
            storage.Save(new SaveData { currency = 42 });

            storage.Backup(SaveData.CURRENT_VERSION);

            Assert.IsTrue(File.Exists($"{_filePath}.v{SaveData.CURRENT_VERSION}.bak"), "No backup was written.");
            Assert.IsTrue(File.Exists(_filePath), "Backing up moved the save instead of copying it.");
            Assert.AreEqual(42, storage.Load().currency);
        }

        [Test]
        public void Backup_WhenNothingSavedYet_DoesNothing()
        {
            Assert.DoesNotThrow(() => NewStorage().Backup(SaveData.CURRENT_VERSION));

            Assert.IsFalse(File.Exists($"{_filePath}.v{SaveData.CURRENT_VERSION}.bak"));
        }

        [Test]
        public void Load_CorruptFile_KeepsItAsideAndReturnsNull()
        {
            File.WriteAllText(_filePath, "this is not json");

            LogAssert.Expect(LogType.Error, CorruptFile);
            SaveData loaded = NewStorage().Load();

            Assert.IsNull(loaded);

            // R14 again: a corrupt save is moved aside, never deleted. It is the only copy of
            // whatever the player had, and in development it is the only way to reproduce this.
            Assert.IsTrue(File.Exists(_filePath + ".corrupt"), "The corrupt save was not kept.");
            Assert.IsFalse(File.Exists(_filePath));
        }

        [Test]
        public void Constructor_EmptyPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new JsonSaveStorage("  "));
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private ISaveStorage NewStorage() => new JsonSaveStorage(_filePath);

        #endregion
    }
}
