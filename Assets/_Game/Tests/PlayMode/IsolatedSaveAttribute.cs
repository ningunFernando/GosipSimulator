using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine.TestTools;
using GosipSimulator.Save;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Gives every test in the fixture its own empty save folder, and points SaveSystem at it before
    /// the test can boot the game. Without it a PlayMode test reads the player's real save.json at
    /// boot (and restores its opinions into the village) and can write it on pause.
    ///
    /// An outer action rather than [SetUp] so it wraps the fixture's own SetUp and TearDown: the
    /// folder exists before anything boots and is deleted only after TearDown has destroyed the
    /// managers. Applied per class because this Test Framework ignores assembly-level test actions;
    /// SaveIsolationTests fails for any fixture that forgets it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class IsolatedSaveAttribute : NUnitAttribute, IOuterUnityTestAction
    {
        private const string FILE_NAME = "save.json";

        /// <summary>The folder the running test saves into. Null outside a test.</summary>
        public static string Folder { get; private set; }

        /// <summary>
        /// Where the running test's save.json lands. Matches SaveSystem's default file name, which
        /// the prefab keeps; a renamed prefab field would make the tests look for the wrong file.
        /// </summary>
        public static string SavePath => Folder != null ? Path.Combine(Folder, FILE_NAME) : null;

        public IEnumerator BeforeTest(ITest test)
        {
            Folder = Path.Combine(Path.GetTempPath(), "GosipSimulatorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);

            SaveSystem.FolderOverride = Folder;

            yield return null;
        }

        public IEnumerator AfterTest(ITest test)
        {
            // Cleared before deleting, so nothing keeps pointing at a folder about to disappear. A
            // run aborted before this line is covered by SaveSystem's reset when Play next starts.
            SaveSystem.FolderOverride = null;

            if (Folder != null && Directory.Exists(Folder)) Directory.Delete(Folder, true);

            Folder = null;

            yield return null;
        }
    }
}
