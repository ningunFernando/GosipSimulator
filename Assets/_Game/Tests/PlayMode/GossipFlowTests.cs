using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// The gossip domain running inside a real game rather than against a constructed graph. These are
    /// what milestone 6 was for: until the adapter existed, GossipService had no call site outside
    /// EditMode, so nothing proved that the assets in Data build a village, that a sighting on the bus
    /// reaches it, or that Update drives its clock.
    ///
    /// The numbers come from the shipped assets and are the same ones the README documents: a robbery
    /// at -10, a village of son to blacksmith at 90, blacksmith to villager at 50, villager to elder
    /// at 40, and 40 percent lost per hop. The witness lands at -10, the blacksmith at -5, the
    /// villager at -1 and the elder never hears it. A failure here is either the adapter or an asset
    /// somebody retuned.
    ///
    /// [IsolatedSave] is what makes those numbers exact: every test boots into an empty save, so no
    /// opinion the player left on disk is restored into the village before the test starts.
    /// </summary>
    [IsolatedSave]
    public class GossipFlowTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;

        // Game time is what rumors travel on, and the shipped hop delay is five seconds. Running the
        // clock fast keeps the suite quick without faking the mechanism the test is here to check.
        private const float SpeedUp = 20f;

        private const string Actor = "player";

        private readonly List<OnRelationshipChanged> _changes = new List<OnRelationshipChanged>();
        private readonly List<OnRumorSpread>         _rumors  = new List<OnRumorSpread>();

        // ────────────────────────────────
        // SETUP AND TEARDOWN
        // ────────────────────────────────
        #region Setup and Teardown

        [SetUp]
        public void SetUp()
        {
            _changes.Clear();
            _rumors.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            // One test pauses the game, and a failure while paused would freeze every test after it.
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
        public IEnumerator SceneGame_BuildsOneGossipManagerFromTheConfiguredVillage()
        {
            yield return BootAndWaitForPlay();

            GossipManager manager = FindSingle<GossipManager>();

            // Awake disables the component when any asset is missing or an id is wrong, so an enabled
            // manager with a service behind it is the whole configuration having been accepted.
            Assert.IsTrue(manager.enabled, "GossipManager disabled itself; the console says which asset it rejected.");
            Assert.IsNotNull(manager.Service, "GossipManager is enabled but built no service.");
            Assert.AreEqual(0, manager.Service.Opinions.Count, "A village nobody has wronged yet holds no opinions.");
        }

        [UnityTest]
        public IEnumerator AWitnessedRobbery_MovesTheWitnessAtOnce_AndPublishesTheChange()
        {
            yield return BootAndWaitForPlay();

            GossipManager manager = FindSingle<GossipManager>();
            SubscribeSinks();

            EventBus.Publish(Sighting("robbery", witnessId: "son", targetId: "blacksmith"));
            yield return null;

            Assert.AreEqual(-10, Opinion(manager, "son"), "The witness did not form an opinion of the thief.");

            Assert.AreEqual(1, _changes.Count, "The sighting published something other than one opinion change.");
            Assert.AreEqual("son", _changes[0].npcId);
            Assert.AreEqual(Actor, _changes[0].aboutId);
            Assert.AreEqual(0, _changes[0].previous);
            Assert.AreEqual(-10, _changes[0].current);
            Assert.AreEqual("robbery", _changes[0].reason, "The reason is what Save writes on the row.");

            // The sighting itself is not a hop, so nothing has been told to anybody yet.
            Assert.AreEqual(0, _rumors.Count, "A rumor was reported before any time passed.");
            Assert.AreEqual(2, manager.Service.PendingCount, "The story should be on its way to the blacksmith and the villager.");
        }

        [UnityTest]
        public IEnumerator TheStoryTravels_ReachingTheBlacksmithAndTheVillagerButNotTheElder()
        {
            yield return BootAndWaitForPlay();

            GossipManager manager = FindSingle<GossipManager>();
            SubscribeSinks();

            Time.timeScale = SpeedUp;

            EventBus.Publish(Sighting("robbery", witnessId: "son", targetId: "blacksmith"));
            yield return WaitUntilTheVillageIsQuiet(manager);

            Assert.AreEqual(-10, Opinion(manager, "son"), "The witness opinion moved after the sighting.");
            Assert.AreEqual(-5, Opinion(manager, "blacksmith"), "The son told his father, and it should land at -5.");
            Assert.AreEqual(-1, Opinion(manager, "villager"), "The story should reach the villager faintly.");

            // Not a gap in the test: the fourth hop truncates to zero, which is what stops a petty
            // theft from becoming village news. Documented in the README table.
            Assert.AreEqual(0, Opinion(manager, "elder"), "The elder heard a story that should have died on the way.");

            Assert.AreEqual(2, _rumors.Count, "Exactly two hops actually moved somebody.");
            Assert.AreEqual(1, _rumors[0].hop);
            Assert.AreEqual("son", _rumors[0].fromId);
            Assert.AreEqual("blacksmith", _rumors[0].toId);
            Assert.AreEqual(2, _rumors[1].hop);
            Assert.AreEqual("villager", _rumors[1].toId);
        }

        [UnityTest]
        public IEnumerator PausingTheGame_HoldsAStoryInFlightUntilItResumes()
        {
            yield return BootAndWaitForPlay();

            GossipManager manager = FindSingle<GossipManager>();
            SubscribeSinks();

            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.AreEqual(GameState.Paused, GameManager.Instance.CurrentState, "The pause request was not honored.");

            EventBus.Publish(Sighting("robbery", witnessId: "son", targetId: "blacksmith"));
            yield return null;

            // Seeing something is not travel, so the witness reacts even while the game is stopped.
            Assert.AreEqual(-10, Opinion(manager, "son"), "The witness did not react during the pause.");
            Assert.AreEqual(2, manager.Service.PendingCount);

            // Realtime on purpose: scaled time is stopped, so WaitForSeconds would never return. Long
            // enough that a Tick on unscaled time would have delivered the first hop by now.
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.AreEqual(0, Opinion(manager, "blacksmith"), "A rumor travelled while the game was paused.");
            Assert.AreEqual(2, manager.Service.PendingCount, "The queue drained while the game was paused.");

            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.AreEqual(GameState.Play, GameManager.Instance.CurrentState, "The game did not resume.");

            Time.timeScale = SpeedUp;
            yield return WaitUntilTheVillageIsQuiet(manager);

            Assert.AreEqual(-5, Opinion(manager, "blacksmith"), "The story did not resume where the pause left it.");
        }

        [UnityTest]
        public IEnumerator RestoredOpinions_AreAppliedWithoutPublishingAChange()
        {
            yield return BootAndWaitForPlay();

            GossipManager manager = FindSingle<GossipManager>();
            SubscribeSinks();

            EventBus.Publish(new OnRelationshipsRestored
            {
                npcIds   = new[] { "blacksmith", "villager" },
                aboutIds = new[] { Actor, Actor },
                values   = new[] { -42, 7 }
            });
            yield return null;

            Assert.AreEqual(-42, Opinion(manager, "blacksmith"), "The saved opinion was not applied.");
            Assert.AreEqual(7, Opinion(manager, "villager"), "The saved opinion was not applied.");

            // The one that matters for milestone 7: Save marks itself dirty on OnRelationshipChanged,
            // so a loud restore would make a game rewrite its own file the moment it finished loading.
            Assert.AreEqual(0, _changes.Count, "Restoring published OnRelationshipChanged, which would dirty a freshly loaded save.");
        }

        [UnityTest]
        public IEnumerator AnActionWithNoDefinition_IsRejectedWithAnErrorAndMovesNobody()
        {
            yield return BootAndWaitForPlay();

            GossipManager manager = FindSingle<GossipManager>();
            SubscribeSinks();

            LogAssert.Expect(LogType.Error, new Regex(@"^\[GossipManager\] No action definition for 'arson'"));

            EventBus.Publish(Sighting("arson", witnessId: "son", targetId: "blacksmith"));
            yield return null;

            Assert.AreEqual(0, manager.Service.Opinions.Count, "An action nobody defined still moved somebody.");
            Assert.AreEqual(0, manager.Service.PendingCount, "An action nobody defined still started a rumor.");
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private IEnumerator BootAndWaitForPlay()
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

        /// <summary>
        /// Records what Gossip published. Save, the shop and the HUD consume both events in the
        /// scene now, so the sinks are here to be read by the assertions, not to keep EventBus quiet.
        /// </summary>
        private void SubscribeSinks()
        {
            EventBus.Subscribe<OnRelationshipChanged>(_changes.Add);
            EventBus.Subscribe<OnRumorSpread>(_rumors.Add);
        }

        private static OnActionWitnessed Sighting(string actionId, string witnessId, string targetId) =>
            new OnActionWitnessed
            {
                actionId  = actionId,
                actorId   = Actor,
                witnessId = witnessId,
                targetId  = targetId
            };

        private static int Opinion(GossipManager manager, string npcId) =>
            manager.Service.Opinions.Get(npcId, Actor);

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

        /// <summary>
        /// Includes inactive on purpose, unlike the other suites: a GossipManager that rejected its
        /// configuration disables itself in Awake, and the default lookup drops disabled components.
        /// Finding it anyway is what turns that into "it disabled itself" instead of "there isn't one".
        /// </summary>
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
