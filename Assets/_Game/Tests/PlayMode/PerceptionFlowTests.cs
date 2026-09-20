using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Gossip;
using GosipSimulator.Npcs;
using GosipSimulator.Save;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Milestone 8: somebody finally sees something. An OnActionCommitted goes onto the bus and the
    /// village answers, which is the half of the chain that did not exist before: until now every
    /// gossip test had to pretend to be perception and publish OnActionWitnessed by hand.
    ///
    /// The last test walks the whole thing end to end, from an action nobody has interpreted yet to
    /// rows on disk, across four assemblies that never reference each other.
    ///
    /// Scene positions these depend on: the son stands at (2, 1, 2), the blacksmith at (-9, 1, 7),
    /// the villager at (9, 1, -7) and the elder at (-10, 1, -10), all with a sight range of 6. Only
    /// the son can see the middle of the map, which is what makes the blacksmith's case work.
    /// </summary>
    public class PerceptionFlowTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;
        private const float  SpeedUp        = 20f;

        private const string Actor = "player";

        private readonly List<OnActionWitnessed> _witnessed = new List<OnActionWitnessed>();
        private readonly List<OnRumorSpread>     _rumors    = new List<OnRumorSpread>();

        private string _tempFolder;
        private string _savePath;

        // ────────────────────────────────
        // SETUP AND TEARDOWN
        // ────────────────────────────────
        #region Setup and Teardown

        [SetUp]
        public void SetUp()
        {
            _witnessed.Clear();
            _rumors.Clear();

            _tempFolder = Path.Combine(Path.GetTempPath(), "GosipSimulatorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
            _savePath = Path.Combine(_tempFolder, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            DestroyAll(Object.FindObjectsByType<GameManager>());
            DestroyAll(Object.FindObjectsByType<ObjectPoolManager>());
            DestroyAll(Object.FindObjectsByType<SaveSystem>());

            EventBus.ClearAllSubscriptions();

            if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [UnityTest]
        public IEnumerator SceneGame_WiresOneRegistryWatchingTheWholeVillage()
        {
            yield return Boot();

            NpcRegistry registry = FindSingle<NpcRegistry>();

            // Awake disables the registry on a duplicate id, an empty slot or an NPC with no
            // definition, so an enabled registry is the whole scene wiring having been accepted.
            Assert.IsTrue(registry.enabled, "NpcRegistry disabled itself; the console says which slot it rejected.");
            Assert.AreEqual(4, registry.Count, "The village should be four NPCs.");

            Npc[] npcs = Object.FindObjectsByType<Npc>(FindObjectsInactive.Include);

            Assert.AreEqual(4, npcs.Length);

            foreach (Npc npc in npcs)
            {
                Assert.IsTrue(npc.enabled, $"'{npc.name}' disabled itself, so it has no identity.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(npc.Id), $"'{npc.name}' has no id.");
            }
        }

        [UnityTest]
        public IEnumerator AnActionInTheMiddleOfTheMap_IsSeenByTheSonAlone()
        {
            yield return Boot();
            SubscribeSinks();

            EventBus.Publish(Committed(Vector3.zero));
            yield return null;

            Assert.AreEqual(1, _witnessed.Count, "Exactly one villager stands close enough to the middle.");
            Assert.AreEqual("son", _witnessed[0].witnessId);
            Assert.AreEqual("robbery", _witnessed[0].actionId, "The action id did not survive perception.");
            Assert.AreEqual(Actor, _witnessed[0].actorId);
            Assert.AreEqual("blacksmith", _witnessed[0].targetId, "The target did not survive perception.");
        }

        [UnityTest]
        public IEnumerator AnActionInTheCorner_IsSeenByNobody()
        {
            yield return Boot();
            SubscribeSinks();

            GossipManager gossip = FindSingle<GossipManager>();

            // Far outside everybody's six units. Being unobserved is the point of the system, so
            // this is the case that has to stay silent rather than the one that has to report.
            EventBus.Publish(Committed(new Vector3(22f, 1f, 22f)));
            yield return null;

            Assert.AreEqual(0, _witnessed.Count, "Somebody saw a theft from across the map.");
            Assert.AreEqual(0, gossip.Service.Opinions.Count, "An unwitnessed action still moved an opinion.");
        }

        [UnityTest]
        public IEnumerator SeveralWitnesses_AreReportedOncePerPersonNearestFirst()
        {
            yield return Boot();
            SubscribeSinks();

            // Walk the elder over to the son's corner. Moving a real NPC rather than faking a
            // candidate list is what makes this a test of the registry and not of the resolver,
            // which has its own suite in EditMode.
            FindNpc("elder").transform.position = new Vector3(4f, 1f, 4f);
            yield return null;

            EventBus.Publish(Committed(Vector3.zero));
            yield return null;

            Assert.AreEqual(2, _witnessed.Count, "One event per witness, so two people means two events.");

            // The son is at (2, 1, 2) and the elder now at (4, 1, 4), so the son is nearer. The order
            // decides who forms an opinion first, which is why it is fixed rather than incidental.
            Assert.AreEqual("son", _witnessed[0].witnessId, "The witnesses did not come back nearest first.");
            Assert.AreEqual("elder", _witnessed[1].witnessId);
        }

        [UnityTest]
        public IEnumerator AnActionWithNoActor_IsRejectedWithAnErrorAndAsksNobody()
        {
            yield return Boot();
            SubscribeSinks();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[NpcRegistry\] An action arrived with an empty id"));

            EventBus.Publish(new OnActionCommitted
            {
                actionId = "robbery",
                actorId  = "",
                targetId = "blacksmith",
                position = Vector3.zero
            });

            yield return null;

            Assert.AreEqual(0, _witnessed.Count, "A malformed action still went out to the village.");
        }

        [UnityTest]
        public IEnumerator TheWholeChain_FromOneActionToRowsOnDisk()
        {
            yield return Boot();
            SubscribeSinks();

            GossipManager gossip = FindSingle<GossipManager>();

            Time.timeScale = SpeedUp;

            // One event. Nobody has been told who saw it, what it is worth, or that a file exists.
            EventBus.Publish(Committed(Vector3.zero));

            yield return WaitUntilTheVillageIsQuiet(gossip);

            // Npcs decided who saw it.
            Assert.AreEqual(1, _witnessed.Count, "Perception did not report the son.");

            // Gossip decided what it was worth and how far it travelled. The same numbers the README
            // publishes, now reached from an action rather than from a hand written sighting.
            Assert.AreEqual(-10, gossip.Service.Opinions.Get("son", Actor), "The witness did not form an opinion.");
            Assert.AreEqual(-5, gossip.Service.Opinions.Get("blacksmith", Actor), "The son did not tell his father.");
            Assert.AreEqual(-1, gossip.Service.Opinions.Get("villager", Actor), "The story did not reach the villager.");
            Assert.AreEqual(0, gossip.Service.Opinions.Get("elder", Actor), "The elder heard a story that should have died.");

            Assert.AreEqual(2, _rumors.Count, "Exactly two hops actually moved somebody.");

            // Save decided what to keep.
            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.IsTrue(File.Exists(_savePath), "Pausing after a theft did not write the save.");

            var onDisk = new RelationshipStore(new JsonSaveStorage(_savePath).Load());

            Assert.AreEqual(3, onDisk.Count, "The file does not hold one row per moved opinion.");
            Assert.AreEqual(-10, onDisk.Get("son", Actor));
            Assert.AreEqual(-5, onDisk.Get("blacksmith", Actor));
            Assert.AreEqual(-1, onDisk.Get("villager", Actor));
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

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

            // Opinions mark the save dirty, so without this a pause would write the player's file.
            SetField(FindSingle<SaveSystem>(), "_storage", new JsonSaveStorage(_savePath));
        }

        /// <summary>
        /// OnRumorSpread still has no consumer in a running game, so without a sink here EventBus
        /// warns about publishing to nobody and the suite grows a warning that means nothing.
        /// </summary>
        private void SubscribeSinks()
        {
            EventBus.Subscribe<OnActionWitnessed>(_witnessed.Add);
            EventBus.Subscribe<OnRumorSpread>(_rumors.Add);
        }

        private static OnActionCommitted Committed(Vector3 position) =>
            new OnActionCommitted
            {
                actionId = "robbery",
                actorId  = Actor,
                targetId = "blacksmith",
                position = position
            };

        private static Npc FindNpc(string id)
        {
            foreach (Npc npc in Object.FindObjectsByType<Npc>(FindObjectsInactive.Include))
            {
                if (string.Equals(npc.Id, id, StringComparison.Ordinal)) return npc;
            }

            Assert.Fail($"No NPC with id '{id}' is in the scene.");
            return null;
        }

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

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} was renamed or removed. Update this test seam.");

            field.SetValue(target, value);
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
