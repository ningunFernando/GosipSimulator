using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GosipSimulator.Actions;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Gossip;
using GosipSimulator.Save;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Milestone 9, and the first time the whole game runs from a key press. E on a virtual keyboard
    /// reaches InteractionReader, which publishes OnActionCommitted; Npcs works out who saw it, Gossip
    /// scores it and spreads it, and Save writes it down. Five assemblies, no references between them.
    ///
    /// Scene facts these depend on: the player starts at (0, 1, 0) with a reach of 2.5, the Strongbox
    /// is at (1, 0.5, 0) carrying Robbery against the blacksmith, and the Well is at (-3, 0.5, 0)
    /// carrying Help. So the strongbox is in reach at spawn and the well is not, which is what makes
    /// walking to it mean something.
    /// </summary>
    [IsolatedSave]
    public class InteractionFlowTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;
        private const float  SpeedUp        = 20f;

        private const string Actor = "player";

        private readonly List<OnActionCommitted> _committed = new List<OnActionCommitted>();
        private readonly List<OnActionWitnessed> _witnessed = new List<OnActionWitnessed>();
        private readonly List<OnRumorSpread>     _rumors    = new List<OnRumorSpread>();

        private Keyboard _keyboard;

#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode _previousBehavior;
#endif

        // ────────────────────────────────
        // SETUP AND TEARDOWN
        // ────────────────────────────────
        #region Setup and Teardown

        [SetUp]
        public void SetUp()
        {
            _committed.Clear();
            _witnessed.Clear();
            _rumors.Clear();

#if UNITY_EDITOR
            // Keyboard input only reaches the game while the Game view has focus by default, and a
            // test run rarely has it (see PauseFlowTests).
            _previousBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);

#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousBehavior;
#endif
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
        public IEnumerator SceneGame_WiresOneReaderAndThreeInteractables()
        {
            yield return Boot();

            InteractionReader reader = FindSingle<InteractionReader>();

            // Awake disables the reader when the input action, the actor id or the list is wrong,
            // so an enabled reader is the whole wiring having been accepted.
            Assert.IsTrue(reader.enabled, "InteractionReader disabled itself; the console says what it rejected.");

            Interactable[] interactables = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include);

            Assert.AreEqual(3, interactables.Length, "The scene should hold the strongbox, the well and the counter.");

            foreach (Interactable interactable in interactables)
            {
                Assert.IsTrue(interactable.enabled, $"'{interactable.name}' disabled itself, so it has no verb.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(interactable.ActionId), $"'{interactable.name}' has no action id.");
            }
        }

        [UnityTest]
        public IEnumerator PressingInteract_NextToTheStrongbox_CommitsARobbery()
        {
            yield return Boot();
            SubscribeSinks();

            yield return Tap(Key.E);

            Assert.AreEqual(1, _committed.Count, "One press should commit exactly one action.");
            Assert.AreEqual("robbery", _committed[0].actionId, "The nearest thing at spawn is the strongbox.");
            Assert.AreEqual(Actor, _committed[0].actorId);
            Assert.AreEqual("blacksmith", _committed[0].targetId, "The strongbox belongs to the blacksmith.");

            // Where the thing is, not where the thief stands: perception asks who could see the act.
            Assert.AreEqual(new Vector3(1f, 0.5f, 0f), _committed[0].position);
        }

        [UnityTest]
        public IEnumerator PressingInteract_WithNothingInReach_CommitsNothing()
        {
            yield return Boot();
            SubscribeSinks();

            // Out in the open, well away from both props and still inside the ground plane.
            FindPlayerBody().position = new Vector3(15f, 1f, 15f);
            yield return new WaitForFixedUpdate();

            yield return Tap(Key.E);

            // Silence is the correct answer, not an error: pressing the key in an empty field is
            // the normal case rather than something to report.
            Assert.AreEqual(0, _committed.Count, "The player acted on something out of reach.");
        }

        [UnityTest]
        public IEnumerator PressingInteract_WhilePaused_CommitsNothing()
        {
            yield return Boot();
            SubscribeSinks();

            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.AreEqual(GameState.Paused, GameManager.Instance.CurrentState, "The pause request was not honored.");

            // Input runs on unscaled time and keeps arriving during a pause, so the state check in
            // the reader is the only thing stopping a paused player from robbing the blacksmith.
            yield return Tap(Key.E);

            Assert.AreEqual(0, _committed.Count, "The player robbed the blacksmith while the game was paused.");
        }

        [UnityTest]
        public IEnumerator WalkingToTheWell_ChangesWhatInteractDoes()
        {
            yield return Boot();
            SubscribeSinks();

            // The well is at (-3, 0.5, 0), out of a reach of 2.5 from the spawn point. Standing
            // beside it puts it in reach and the strongbox out of it, so the same key does the
            // opposite thing. That is the reach rule doing its job in a real scene.
            FindPlayerBody().position = new Vector3(-3f, 1f, 0f);
            yield return new WaitForFixedUpdate();

            yield return Tap(Key.E);

            Assert.AreEqual(1, _committed.Count, "Standing at the well, the key did nothing.");
            Assert.AreEqual("help", _committed[0].actionId, "The same key still committed a robbery.");
        }

        [UnityTest]
        public IEnumerator OneKeyPress_TravelsAllTheWayToRowsOnDisk()
        {
            yield return Boot();
            SubscribeSinks();

            GossipManager gossip = FindSingle<GossipManager>();

            Time.timeScale = SpeedUp;

            // The whole game, from here on, out of one key press.
            yield return Tap(Key.E);
            yield return WaitUntilTheVillageIsQuiet(gossip);

            Assert.AreEqual(1, _committed.Count, "Actions did not commit the robbery.");
            Assert.AreEqual(1, _witnessed.Count, "Npcs did not report the son as a witness.");
            Assert.AreEqual("son", _witnessed[0].witnessId);

            // The numbers the README publishes, now reached by pressing a key.
            Assert.AreEqual(-10, gossip.Service.Opinions.Get("son", Actor), "The witness did not form an opinion.");
            Assert.AreEqual(-5, gossip.Service.Opinions.Get("blacksmith", Actor), "The son did not tell his father.");
            Assert.AreEqual(-1, gossip.Service.Opinions.Get("villager", Actor), "The story did not reach the villager.");
            Assert.AreEqual(0, gossip.Service.Opinions.Get("elder", Actor), "The elder heard a story that should have died.");

            EventBus.Publish(new OnPauseRequested());
            yield return null;

            var onDisk = new RelationshipStore(new JsonSaveStorage(IsolatedSaveAttribute.SavePath).Load());

            Assert.AreEqual(3, onDisk.Count, "The file does not hold one row per moved opinion.");
            Assert.AreEqual(-10, onDisk.Get("son", Actor));
            Assert.AreEqual(-5, onDisk.Get("blacksmith", Actor));
        }

        [UnityTest]
        public IEnumerator HelpingAtTheWell_MovesTheSonTheOtherWay()
        {
            yield return Boot();
            SubscribeSinks();

            GossipManager gossip = FindSingle<GossipManager>();

            // The well is 5.41 from the son, inside his sight range of 6, so a good turn is seen
            // too. Help is worth +20, which is the same machinery running in the opposite direction.
            FindPlayerBody().position = new Vector3(-3f, 1f, 0f);
            yield return new WaitForFixedUpdate();

            Time.timeScale = SpeedUp;

            yield return Tap(Key.E);
            yield return WaitUntilTheVillageIsQuiet(gossip);

            Assert.AreEqual("help", _committed[0].actionId);
            Assert.AreEqual(20, gossip.Service.Opinions.Get("son", Actor), "Helping did not improve the son's opinion.");
            Assert.AreEqual(10, gossip.Service.Opinions.Get("blacksmith", Actor), "The good word did not reach the father.");
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
        }

        /// <summary>Press and release across a few frames, so the action sees both edges.</summary>
        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
            yield return null;
            yield return null;

            InputSystem.QueueStateEvent(_keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            yield return null;
        }

        /// <summary>
        /// Records what the key press set off. Since milestone 11 the HUD consumes OnRumorSpread too,
        /// so these sinks are here to be read by the assertions, not to keep EventBus quiet.
        /// </summary>
        private void SubscribeSinks()
        {
            EventBus.Subscribe<OnActionCommitted>(_committed.Add);
            EventBus.Subscribe<OnActionWitnessed>(_witnessed.Add);
            EventBus.Subscribe<OnRumorSpread>(_rumors.Add);
        }

        private static Rigidbody FindPlayerBody()
        {
            Rigidbody body = FindSingle<InteractionReader>().GetComponent<Rigidbody>();

            Assert.IsTrue(body != null, "The reader is not on the player, so there is nothing to move.");

            return body;
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
