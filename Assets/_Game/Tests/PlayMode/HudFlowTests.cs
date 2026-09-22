using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Debug;
using GosipSimulator.Gossip;
using GosipSimulator.Save;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Milestone 11: seeing it work without a debugger. The HUD is the one place that hears from
    /// Gossip, Shop and Save at once, and every line on it is a copy one of them pushed (R7).
    ///
    /// No sinks are subscribed here on purpose. The HUD is supposed to be the consumer now, so if a
    /// rumor or a shop event reaches the bus with nobody listening, the warning EventBus logs is
    /// caught and fails the test. That is the check that OnRumorSpread finally has a home.
    ///
    /// [IsolatedSave] boots every test into an empty temporary save, so the village starts neutral
    /// and the HUD's first lines come from the real boot rather than from the player's file.
    /// </summary>
    [IsolatedSave]
    public class HudFlowTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;
        private const float  SpeedUp        = 20f;

        private const string Customer   = "player";
        private const string Blacksmith = "blacksmith";
        private const string NoOneHeard = "with no subscribers";

        private readonly List<string> _warnings = new List<string>();

        // ────────────────────────────────
        // SETUP AND TEARDOWN
        // ────────────────────────────────
        #region Setup and Teardown

        [SetUp]
        public void SetUp()
        {
            _warnings.Clear();

            Application.logMessageReceived += CaptureWarning;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CaptureWarning;

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
        public IEnumerator AtBoot_TheHudShowsWhatTheShopCharges()
        {
            yield return Boot();

            // Nothing published these terms but the boot itself: Save restored an empty file, the
            // shop answered with its starting terms, and the HUD was already listening.
            StringAssert.Contains("blacksmith: 5 (100%)", HudText(), "The starting terms never reached the HUD.");
        }

        [UnityTest]
        public IEnumerator ARobbery_ShowsUpAsOpinionsRumorsAndAPrice()
        {
            yield return Boot();

            GossipManager gossip = FindSingle<GossipManager>();

            Time.timeScale = SpeedUp;

            EventBus.Publish(new OnActionWitnessed
            {
                actionId  = "robbery",
                actorId   = Customer,
                witnessId = "son",
                targetId  = Blacksmith
            });

            yield return WaitUntilTheVillageIsQuiet(gossip);

            string text = HudText();

            // The README's numbers, on screen. The son saw it, the blacksmith heard it and charges
            // for it, and the villager heard a fainter version.
            StringAssert.Contains("son -> player: -10 (robbery)", text, "The witness's opinion is not on the HUD.");
            StringAssert.Contains("blacksmith -> player: -5 (robbery)", text, "The rumor's effect is not on the HUD.");
            StringAssert.Contains("son -> blacksmith: robbery, hop 1, -5", text, "The rumor itself is not on the HUD.");
            StringAssert.Contains("blacksmith: 6 (110%)", text, "The new price is not on the HUD.");

            AssertNobodyWasIgnored();
        }

        [UnityTest]
        public IEnumerator APurchase_ShowsUpAsTheLastPurchaseAndTheNewBalance()
        {
            yield return Boot();

            FindSingle<SaveSystem>().Progress.Earn(9);

            Trade();
            yield return null;

            string text = HudText();

            StringAssert.Contains("Last purchase: bought horseshoe from blacksmith for 5", text);
            StringAssert.Contains("Currency: 4", text, "The spending did not reach the HUD.");

            AssertNobodyWasIgnored();
        }

        [UnityTest]
        public IEnumerator ARefusal_ShowsUpOnTheHud()
        {
            yield return Boot();

            RestoreBlacksmithOpinion(-40);
            yield return null;

            Trade();
            yield return null;

            string text = HudText();

            StringAssert.Contains("blacksmith: refuses to sell", text);
            StringAssert.Contains("Last purchase: blacksmith refused to sell horseshoe (opinion -40)", text);

            AssertNobodyWasIgnored();
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

            // Whatever the boot itself logged is not what these tests are about.
            _warnings.Clear();
        }

        private static void RestoreBlacksmithOpinion(int value)
        {
            EventBus.Publish(new OnRelationshipsRestored
            {
                npcIds   = new[] { Blacksmith },
                aboutIds = new[] { Customer },
                values   = new[] { value }
            });
        }

        /// <summary>What pressing the key at the counter publishes, without walking there.</summary>
        private static void Trade()
        {
            EventBus.Publish(new OnActionCommitted
            {
                actionId = "trade",
                actorId  = Customer,
                targetId = Blacksmith,
                position = new Vector3(-7f, 0.5f, 6f)
            });
        }

        private static string HudText()
        {
            DebugHud hud   = FindSingle<DebugHud>();
            Label    label = hud.GetComponent<UIDocument>().rootVisualElement.Q<Label>(DebugHud.LABEL_NAME);

            Assert.IsNotNull(label, "The HUD label is not in the UIDocument tree.");

            return label.text;
        }

        private void CaptureWarning(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning && message.Contains(NoOneHeard)) _warnings.Add(message);
        }

        private void AssertNobodyWasIgnored()
        {
            Assert.IsEmpty(_warnings, "Something was published with nobody listening: " + string.Join(" | ", _warnings));
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
