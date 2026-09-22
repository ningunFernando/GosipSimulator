using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Gossip;
using GosipSimulator.Save;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Milestone 7 end to end: an opinion reaching the file, and a file reaching the village. The two
    /// modules never touch. Gossip publishes OnRelationshipChanged without knowing a file exists, Save
    /// writes a row without knowing what a rumor is, and on the way back Save pushes
    /// OnRelationshipsRestored because Gossip cannot ask it anything (R3, R4).
    ///
    /// [IsolatedSave] boots every test into an empty temporary folder, so the player's real save is
    /// never read or written, and "what is on disk" always means what the test put there.
    /// </summary>
    [IsolatedSave]
    public class RelationshipPersistenceTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;

        private const string Actor = "player";

        private readonly List<OnRumorSpread> _rumors = new List<OnRumorSpread>();

        private static string SavePath => IsolatedSaveAttribute.SavePath;

        // ────────────────────────────────
        // SETUP AND TEARDOWN
        // ────────────────────────────────
        #region Setup and Teardown

        [SetUp]
        public void SetUp()
        {
            _rumors.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

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

        [UnityTest]
        public IEnumerator AWitnessedRobbery_EndsUpAsRowsInTheSaveFile()
        {
            yield return Boot();

            GossipManager manager = FindSingle<GossipManager>();
            SaveSystem    save    = FindSingle<SaveSystem>();

            EventBus.Subscribe<OnRumorSpread>(_rumors.Add);

            Time.timeScale = SpeedUp;

            EventBus.Publish(new OnActionWitnessed
            {
                actionId  = "robbery",
                actorId   = Actor,
                witnessId = "son",
                targetId  = "blacksmith"
            });

            yield return WaitUntilTheVillageIsQuiet(manager);

            Assert.IsFalse(File.Exists(SavePath), "The save was written before anything asked for it.");

            // Pausing is the in game checkpoint, and the save is dirty because opinions moved.
            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.AreEqual(GameState.Paused, GameManager.Instance.CurrentState);
            Assert.IsTrue(File.Exists(SavePath), "Pausing with moved opinions did not write the save.");

            SaveData stored = new JsonSaveStorage(SavePath).Load();

            Assert.AreEqual(SaveData.CURRENT_VERSION, stored.saveVersion, "The file was written at the wrong version.");

            var onDisk = new RelationshipStore(stored);

            // The same three numbers the README publishes, now read back off the disk.
            Assert.AreEqual(-10, onDisk.Get("son", Actor), "The witness opinion did not reach the file.");
            Assert.AreEqual(-5, onDisk.Get("blacksmith", Actor), "The rumor reached the blacksmith but not the file.");
            Assert.AreEqual(-1, onDisk.Get("villager", Actor), "The rumor reached the villager but not the file.");

            // Sparse on disk as well as in memory: the elder never heard it, so he has no row.
            Assert.AreEqual(3, onDisk.Count, "The file holds a row for somebody who never heard the story.");

            Assert.AreEqual("robbery", stored.relationships[0].reason, "The row lost what caused it.");
            Assert.AreEqual(save.Relationships.Count, onDisk.Count, "Memory and disk disagree on how many opinions exist.");
        }

        [UnityTest]
        public IEnumerator OpinionsOnDisk_AreRestoredToTheVillageOnBootstrap()
        {
            yield return Boot();

            GossipManager manager = FindSingle<GossipManager>();
            SaveSystem    save    = FindSingle<SaveSystem>();

            // Values nothing in this session could have produced, so finding them in the graph can
            // only mean they came off the disk.
            WriteSave(new SaveData
            {
                currency    = 55,
                totalEarned = 55,
                relationships = new List<RelationshipRow>
                {
                    new RelationshipRow { npcId = "elder", aboutId = Actor, value = -77, reason = "robbery" },
                    new RelationshipRow { npcId = "villager", aboutId = Actor, value = 34, reason = "help" }
                }
            });

            Assert.AreEqual(0, manager.Service.Opinions.Count, "The village started with opinions it should not have.");

            save.Load();

            // The same event the Bootstrapper publishes once the game scene is up and its objects
            // have subscribed in OnEnable. GossipManager is listening, so nothing here reaches into it.
            EventBus.Publish(new OnBootstrapComplete());
            yield return null;

            Assert.AreEqual(-77, manager.Service.Opinions.Get("elder", Actor), "The saved opinion never reached the village.");
            Assert.AreEqual(34, manager.Service.Opinions.Get("villager", Actor), "The saved opinion never reached the village.");
            Assert.AreEqual(2, manager.Service.Opinions.Count, "Restoring invented opinions nobody saved.");

            Assert.AreEqual(55, save.Progress.Currency, "Currency and opinions did not come from the same file.");
        }

        [UnityTest]
        public IEnumerator RestoringASave_DoesNotMarkItDirtyAgain()
        {
            yield return Boot();

            SaveSystem save = FindSingle<SaveSystem>();

            WriteSave(new SaveData
            {
                relationships = new List<RelationshipRow>
                {
                    new RelationshipRow { npcId = "blacksmith", aboutId = Actor, value = -20, reason = "robbery" }
                }
            });

            save.Load();
            EventBus.Publish(new OnBootstrapComplete());
            yield return null;

            File.Delete(SavePath);

            // If Restore had published OnRelationshipChanged per row, Save would have marked itself
            // dirty and a freshly loaded game would rewrite its own file on the next pause. That is
            // the reason GossipService.Restore is silent, and this is the test that holds it to it.
            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.AreEqual(GameState.Paused, GameManager.Instance.CurrentState);
            Assert.IsFalse(File.Exists(SavePath), "Loading a game marked it dirty and it rewrote itself on pause.");
        }

        [UnityTest]
        public IEnumerator AVersion1Save_IsMigratedAndBackedUpInsteadOfReplaced()
        {
            yield return Boot();

            SaveSystem save = FindSingle<SaveSystem>();

            // Written as raw JSON, the way a build from before the village would have left it: no
            // relationships field at all. Building it from the current class could not reproduce that.
            const string v1Json = "{\"saveVersion\":1,\"currency\":250,\"totalEarned\":900}";

            File.WriteAllText(SavePath, v1Json);

            save.Load();

            Assert.AreEqual(250, save.Progress.Currency, "The migration lost the player's currency.");
            Assert.AreEqual(900, save.Progress.TotalEarned, "The migration lost the lifetime accumulator.");
            Assert.AreEqual(0, save.Relationships.Count, "A v1 save cannot have had opinions.");

            // R14: migrated, never deleted. The old file is kept beside the new one.
            Assert.IsTrue(File.Exists(SavePath + ".v1.bak"), "The v1 file was migrated without a backup.");

            // Byte for byte, because Backup copies the file rather than reserialising the object.
            // That matters: a rewritten backup would be the migrated shape wearing the old version
            // number, and the original the player actually had would be gone.
            Assert.AreEqual(v1Json, File.ReadAllText(SavePath + ".v1.bak"),
                "The backup is not a faithful copy of the original v1 file.");

            yield return null;
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private const float SpeedUp = 20f;

        private IEnumerator Boot()
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

        private static void WriteSave(SaveData data) => new JsonSaveStorage(SavePath).Save(data);

        private static IEnumerator WaitUntilTheVillageIsQuiet(GossipManager manager)
        {
            int frames = 0;

            while (frames < MaxFrames && manager.Service.PendingCount > 0)
            {
                frames++;
                yield return null;
            }

            Assert.AreEqual(0, manager.Service.PendingCount,
                $"The village was still passing the story on after {MaxFrames} frames.");
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
