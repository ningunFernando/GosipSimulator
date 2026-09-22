using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Save;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The guard for every other PlayMode test. They boot the real game, and the real game loads
    /// the player's save.json before a test can reach the SaveSystem it created, so a test that
    /// forgets [IsolatedSave] reads a stranger's opinions into the village and asserts numbers they
    /// have already moved. These tests make forgetting fail loudly instead of intermittently.
    /// </summary>
    [IsolatedSave]
    public class SaveIsolationTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;

        // ────────────────────────────────
        // TEARDOWN
        // ────────────────────────────────
        #region Teardown

        [TearDown]
        public void TearDown()
        {
            DestroyAll(Object.FindObjectsByType<GameManager>());
            DestroyAll(Object.FindObjectsByType<ObjectPoolManager>());
            DestroyAll(Object.FindObjectsByType<SaveSystem>());

            EventBus.ClearAllSubscriptions();
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void EveryPlayModeFixture_IsolatesTheSave()
        {
            // Every fixture rather than only the ones that boot today: a fixture that starts booting
            // later would not remember to add it, and the attribute costs nothing where unused.
            var missing  = new List<string>();
            int fixtures = 0;

            foreach (Type type in typeof(SaveIsolationTests).Assembly.GetTypes())
            {
                if (!HasTests(type)) continue;

                fixtures++;

                if (type.GetCustomAttribute<IsolatedSaveAttribute>(true) == null) missing.Add(type.Name);
            }

            // Guards the guard: finding no fixtures would pass this test for the wrong reason.
            Assert.Greater(fixtures, 1, "The reflection found no test fixtures, so it checked nothing.");
            Assert.IsEmpty(missing, "These fixtures can read or write the player's real save.json: " +
                                    string.Join(", ", missing));
        }

        [Test]
        public void TheSeam_IsClearedWheneverPlayStarts()
        {
            // Enter Play Mode Options keep statics between Play sessions here, so without this reset
            // an aborted test run would send the next real session's saves to a deleted folder.
            MethodInfo reset = typeof(SaveSystem).GetMethod("ClearFolderOverride",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(reset, "SaveSystem.ClearFolderOverride was renamed or removed.");

            var attribute = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

            Assert.IsNotNull(attribute, "ClearFolderOverride lost [RuntimeInitializeOnLoadMethod], so Unity never runs it.");
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType,
                "The reset has to run before any scene loads, or the Bootstrapper could read a stale folder.");

            string ours = SaveSystem.FolderOverride;

            try
            {
                reset.Invoke(null, null);
                Assert.IsNull(SaveSystem.FolderOverride, "The reset left the override in place.");
            }
            finally
            {
                SaveSystem.FolderOverride = ours;
            }
        }

        [UnityTest]
        public IEnumerator ABootedGame_ReadsAndWritesTheTestFolder_NeverTheRealSave()
        {
            string realPath    = Path.Combine(Application.persistentDataPath, "save.json");
            bool   realExisted = File.Exists(realPath);
            DateTime realWrite = realExisted ? File.GetLastWriteTimeUtc(realPath) : default;

            // A balance nothing else produces, written where the test says the save lives. Finding
            // it after boot can only mean the Bootstrapper's Load read this folder.
            new JsonSaveStorage(IsolatedSaveAttribute.SavePath).Save(new SaveData { currency = 42, totalEarned = 42 });

            yield return Boot();

            SaveSystem save = FindSingle<SaveSystem>();

            Assert.AreEqual(42, save.Progress.Currency, "The boot did not load the test's save, so it read another one.");

            save.Progress.Earn(3);
            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.AreEqual(GameState.Paused, GameManager.Instance.CurrentState);
            Assert.AreEqual(45, new JsonSaveStorage(IsolatedSaveAttribute.SavePath).Load().currency,
                "Pausing did not write into the test folder.");

            Assert.AreEqual(realExisted, File.Exists(realPath), "A test created or deleted the player's real save.");

            if (realExisted)
            {
                Assert.AreEqual(realWrite, File.GetLastWriteTimeUtc(realPath), "A test rewrote the player's real save.");
            }
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private static bool HasTests(Type type)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public))
            {
                if (method.IsDefined(typeof(TestAttribute), true) ||
                    method.IsDefined(typeof(UnityTestAttribute), true) ||
                    method.IsDefined(typeof(TestCaseAttribute), true))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerator Boot()
        {
            SceneManager.LoadScene(BootstrapScene);
            yield return null;

            int frames = 0;

            while (frames < MaxFrames &&
                   !(GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Play))
            {
                frames++;
                yield return null;
            }

            Assert.IsTrue(GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Play,
                "The bootstrap never reached Play; see BootstrapSequenceTests.");
        }

        private static T FindSingle<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            Assert.AreEqual(1, found.Length, $"Expected exactly one {typeof(T).Name}.");

            return found[0];
        }

        private static void DestroyAll<T>(T[] components) where T : Component
        {
            foreach (T component in components)
            {
                if (component != null) Object.Destroy(component.gameObject);
            }
        }

        #endregion
    }
}
