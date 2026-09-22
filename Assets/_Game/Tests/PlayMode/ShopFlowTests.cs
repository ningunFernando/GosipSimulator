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
using GosipSimulator.Shop;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Milestone 10: the consequence. A robbery the blacksmith only heard about makes the next
    /// horseshoe dearer, and a bad enough reputation closes the shop. Shop never asks Gossip anything
    /// and Save never learns why a price is what it is; every step below is a bus event (R3, R4).
    ///
    /// Scene facts these depend on: the Counter is at (-7, 0.5, 6) carrying the trade action against
    /// the blacksmith, who stands at (-9, 1, 7) and is the only NPC close enough to see it. The shop
    /// sells one horseshoe at a base price of 5, 2% per point of opinion, refusing at -30.
    ///
    /// [IsolatedSave] boots every test into an empty temporary save, so the blacksmith starts neutral,
    /// the purse starts at zero, and nothing depends on the player's real file.
    /// </summary>
    [IsolatedSave]
    public class ShopFlowTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;
        private const float  SpeedUp        = 20f;

        private const string Customer   = "player";
        private const string Blacksmith = "blacksmith";
        private const int    BasePrice  = 5;

        private static readonly Vector3 BesideTheCounter = new Vector3(-6f, 1f, 6f);

        private readonly List<OnShopTermsChanged> _terms    = new List<OnShopTermsChanged>();
        private readonly List<OnPurchaseApproved> _approved = new List<OnPurchaseApproved>();
        private readonly List<OnPurchaseRefused>  _refused  = new List<OnPurchaseRefused>();
        private readonly List<OnPurchaseSettled>  _settled  = new List<OnPurchaseSettled>();

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
            _terms.Clear();
            _approved.Clear();
            _refused.Clear();
            _settled.Clear();

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
        public IEnumerator SceneGame_HasOneOpenShopRunByTheBlacksmith()
        {
            yield return Boot();

            Shopkeeper shop = FindSingle<Shopkeeper>();

            // Awake disables the shop when the NPC, the trade action or the pricing is wrong, so an
            // enabled one is the whole configuration having been accepted.
            Assert.IsTrue(shop.enabled, "Shopkeeper disabled itself; the console says what it rejected.");
            Assert.AreEqual(Blacksmith, shop.ShopkeeperId);
        }

        [UnityTest]
        public IEnumerator ARestore_EvenAnEmptyOne_TellsListenersTheTerms()
        {
            yield return Boot();

            // What Save publishes for a fresh game: null arrays. A restore always publishes the
            // terms, even when the price did not move, so the HUD starts from real terms instead of
            // an assumption. The boot already did this once, before the sinks were listening.
            EventBus.Publish(new OnRelationshipsRestored());
            yield return null;

            Assert.AreEqual(1, _terms.Count, "The restore did not publish the starting terms.");
            Assert.AreEqual(Blacksmith, _terms[0].shopkeeperId);
            Assert.AreEqual(Customer, _terms[0].customerId);
            Assert.AreEqual(100, _terms[0].pricePercent);
            Assert.AreEqual(BasePrice, _terms[0].price);
            Assert.IsFalse(_terms[0].refuses);
        }

        [UnityTest]
        public IEnumerator TheBlacksmithSeeingARobbery_RaisesThePrice()
        {
            yield return Boot();

            Shopkeeper shop = FindSingle<Shopkeeper>();

            EventBus.Publish(new OnActionWitnessed
            {
                actionId  = "robbery",
                actorId   = Customer,
                witnessId = Blacksmith,
                targetId  = Blacksmith
            });

            yield return null;

            // Seen first hand, so the full -10: 120% of 5 is exactly 6.
            Assert.AreEqual(-10, shop.Opinion, "The opinion change never reached the shop.");
            Assert.AreEqual(120, shop.Terms.Percent);
            Assert.AreEqual(6, shop.Terms.Price);

            OnShopTermsChanged last = _terms[_terms.Count - 1];

            Assert.AreEqual(6, last.price, "The new price was not published.");
            Assert.AreEqual(-10, last.opinion);
        }

        [UnityTest]
        public IEnumerator ARobberyTheBlacksmithOnlyHeardAbout_StillRaisesThePrice()
        {
            yield return Boot();

            Shopkeeper    shop   = FindSingle<Shopkeeper>();
            GossipManager gossip = FindSingle<GossipManager>();

            Time.timeScale = SpeedUp;

            // The whole point of the game. The player robs the strongbox with only the son looking,
            // the son tells the blacksmith, and the price goes up without the blacksmith seeing a thing.
            yield return Tap(Key.E);
            yield return WaitUntilTheVillageIsQuiet(gossip);

            Assert.AreEqual(-5, shop.Opinion, "The rumor did not reach the shop's copy of the opinion.");
            Assert.AreEqual(110, shop.Terms.Percent);
            Assert.AreEqual(6, shop.Terms.Price, "110% of 5 rounds up to 6.");
        }

        [UnityTest]
        public IEnumerator PressingInteract_AtTheCounter_BuysAtTheCurrentPrice()
        {
            yield return Boot();

            SaveSystem save = FindSingle<SaveSystem>();
            save.Progress.Earn(20);

            FindPlayerBody().position = BesideTheCounter;
            yield return new WaitForFixedUpdate();

            yield return Tap(Key.E);

            Assert.AreEqual(1, _approved.Count, "The counter did not ask the blacksmith to sell.");
            Assert.AreEqual(BasePrice, _approved[0].price);
            Assert.AreEqual("horseshoe", _approved[0].itemId);

            Assert.AreEqual(1, _settled.Count, "Save did not settle the purchase.");
            Assert.IsTrue(_settled[0].paid);
            Assert.AreEqual(20 - BasePrice, save.Progress.Currency, "The price was not taken.");
        }

        [UnityTest]
        public IEnumerator BuyingWithoutEnoughCurrency_SettlesUnpaid_AndTakesNothing()
        {
            yield return Boot();

            SaveSystem save = FindSingle<SaveSystem>();
            save.Progress.Earn(BasePrice - 1);

            Trade();
            yield return null;

            // The blacksmith agreed; the purse said no. Two owners, two answers, and the second one
            // is an event rather than silence so the player learns why nothing happened.
            Assert.AreEqual(1, _approved.Count, "The shop should agree to sell; affording it is not its call.");
            Assert.AreEqual(1, _settled.Count, "A purchase that could not be paid for ended in silence.");
            Assert.IsFalse(_settled[0].paid);
            Assert.AreEqual(BasePrice - 1, save.Progress.Currency, "A failed purchase still took money.");
        }

        [UnityTest]
        public IEnumerator AHatedCustomer_IsRefused_AndNothingReachesSave()
        {
            yield return Boot();

            SaveSystem save = FindSingle<SaveSystem>();
            save.Progress.Earn(100);

            RestoreBlacksmithOpinion(-40);
            yield return null;

            Assert.IsTrue(FindSingle<Shopkeeper>().Terms.Refuses, "At -40 the shop should be closed.");

            Trade();
            yield return null;

            Assert.AreEqual(1, _refused.Count, "The refusal was not published.");
            Assert.AreEqual(-40, _refused[0].opinion, "The refusal does not say why.");
            Assert.AreEqual(0, _approved.Count, "A closed shop still approved a sale.");
            Assert.AreEqual(0, _settled.Count, "Save settled a purchase nobody approved.");
            Assert.AreEqual(100, save.Progress.Currency, "A refused customer was charged.");
        }

        [UnityTest]
        public IEnumerator AGrudgeInTheSaveFile_IsChargedFromTheStart()
        {
            // Written before the boot, so the real sequence loads it: Bootstrapper calls Load, and
            // Save restores it on OnBootstrapComplete. Restoring is silent on purpose (no
            // OnRelationshipChanged per row), so this is the path that would have left a loaded
            // grudge invisible until the next rumor if the shop only listened to changes.
            new JsonSaveStorage(IsolatedSaveAttribute.SavePath).Save(new SaveData
            {
                relationships = new List<RelationshipRow>
                {
                    new RelationshipRow { npcId = Blacksmith, aboutId = Customer, value = -20, reason = "robbery" }
                }
            });

            yield return Boot();

            Shopkeeper shop = FindSingle<Shopkeeper>();

            Assert.AreEqual(-20, shop.Opinion, "The saved opinion never reached the shop.");
            Assert.AreEqual(140, shop.Terms.Percent);
            Assert.AreEqual(7, shop.Terms.Price, "140% of 5 is exactly 7.");
        }

        [UnityTest]
        public IEnumerator ABuyingSpree_IsWrittenToTheSaveOnPause()
        {
            yield return Boot();

            SaveSystem save = FindSingle<SaveSystem>();
            save.Progress.Earn(12);

            Trade();
            Trade();
            yield return null;

            Assert.AreEqual(2, _settled.Count);
            Assert.AreEqual(2, save.Progress.Currency, "Two horseshoes at 5 from 12 should leave 2.");

            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.AreEqual(GameState.Paused, GameManager.Instance.CurrentState);
            Assert.AreEqual(2, new JsonSaveStorage(IsolatedSaveAttribute.SavePath).Load().currency, "The spending never reached the file.");
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        /// <summary>
        /// Boots the real game into the test's save and subscribes the sinks. Anything published
        /// during the boot itself, such as the starting terms, happened before they were listening.
        /// </summary>
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

            EventBus.Subscribe<OnShopTermsChanged>(_terms.Add);
            EventBus.Subscribe<OnPurchaseApproved>(_approved.Add);
            EventBus.Subscribe<OnPurchaseRefused>(_refused.Add);
            EventBus.Subscribe<OnPurchaseSettled>(_settled.Add);
        }

        /// <summary>
        /// The same event Save publishes at boot. Gossip and the shop both apply it, so the village
        /// and the price agree on where the blacksmith stands.
        /// </summary>
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
            Interactable counter = FindCounter();

            EventBus.Publish(new OnActionCommitted
            {
                actionId = counter.ActionId,
                actorId  = Customer,
                targetId = counter.TargetId,
                position = counter.transform.position
            });
        }

        private static Interactable FindCounter()
        {
            foreach (Interactable interactable in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include))
            {
                if (interactable.ActionId == "trade") return interactable;
            }

            Assert.Fail("No interactable in the scene carries the trade action.");
            return null;
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
