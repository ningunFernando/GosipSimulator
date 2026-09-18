using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Debug;
using GosipSimulator.Pickups;
using GosipSimulator.Player;
using GosipSimulator.Save;
using Object = UnityEngine.Object;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The loop that exercises pool, bus, save and HUD together at runtime: the spawner takes pickups
    /// from the pool, the player touches one, Save turns OnPickupCollected into currency, the HUD shows
    /// it, the pickup goes back to the pool and returns to its point, and pausing writes the save. The
    /// save is redirected to a temporary file, so the real one is never written by a test.
    /// </summary>
    public class PickupFlowTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;
        private const float  RespawnTimeout = 5f;

        private string _tempFolder;

        // ────────────────────────────────
        // TEARDOWN
        // ────────────────────────────────
        #region Teardown

        [TearDown]
        public void TearDown()
        {
            // Pausing is part of one test; a failure while paused would freeze every later test.
            Time.timeScale = 1f;

            DestroyAll(Object.FindObjectsByType<GameManager>());
            DestroyAll(Object.FindObjectsByType<ObjectPoolManager>());
            DestroyAll(Object.FindObjectsByType<SaveSystem>());

            EventBus.ClearAllSubscriptions();

            if (_tempFolder != null && Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);
            _tempFolder = null;
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [UnityTest]
        public IEnumerator PlayerTouchingPickup_GrantsCurrencyAndPickupReturnsToItsPoint()
        {
            yield return BootAndWaitForPlay();

            SaveSystem save   = FindSingle<SaveSystem>();
            Pickup     target = FindAnyActivePickup();
            Vector3    spot   = target.transform.position;
            int        before = save.Progress.Currency;
            int        value  = target.Value;

            Rigidbody player = FindSingle<PlayerMover>().GetComponent<Rigidbody>();
            Vector3   home   = player.position;

            yield return TeleportAndStep(player, spot);

            Assert.IsFalse(target.gameObject.activeSelf, "The collected pickup was not returned to the pool.");
            Assert.AreEqual(before + value, save.Progress.Currency, "Collecting did not grant the pickup's value.");
            StringAssert.Contains($"Currency: {before + value}", HudText(), "OnProgressChanged did not reach the HUD.");

            // Step away first: a pickup that respawns under the player is collected again at once.
            yield return TeleportAndStep(player, home);

            float waited = 0f;

            while (waited < RespawnTimeout && FindActivePickupAt(spot) == null)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.IsNotNull(FindActivePickupAt(spot), $"No pickup came back to its point within {RespawnTimeout} s.");
        }

        [UnityTest]
        public IEnumerator PausingAfterCollecting_WritesProgressToTheSave()
        {
            yield return BootAndWaitForPlay();

            _tempFolder = Path.Combine(Path.GetTempPath(), "GosipSimulatorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
            string savePath = Path.Combine(_tempFolder, "save.json");

            SaveSystem save = FindSingle<SaveSystem>();
            SetField(save, "_storage", new JsonSaveStorage(savePath));

            Rigidbody player = FindSingle<PlayerMover>().GetComponent<Rigidbody>();
            yield return TeleportAndStep(player, FindAnyActivePickup().transform.position);

            Assert.IsFalse(File.Exists(savePath), "The save was written before the game was paused.");

            EventBus.Publish(new OnPauseRequested());
            yield return null;

            Assert.AreEqual(GameState.Paused, GameManager.Instance.CurrentState, "The pause request was not honored.");
            Assert.IsTrue(File.Exists(savePath), "Pausing with unsaved progress did not write the save.");

            SaveData stored = new JsonSaveStorage(savePath).Load();

            Assert.AreEqual(save.Progress.Currency, stored.currency);
            Assert.AreEqual(save.Progress.TotalEarned, stored.totalEarned);
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private static IEnumerator BootAndWaitForPlay()
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
        /// Moves the body directly and lets physics run, which is what fires OnTriggerEnter. The
        /// player keeps its own height, so it stays on the ground instead of sinking into it.
        /// </summary>
        private static IEnumerator TeleportAndStep(Rigidbody body, Vector3 point)
        {
            body.position        = new Vector3(point.x, body.position.y, point.z);
            body.linearVelocity  = Vector3.zero;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        private static Pickup FindAnyActivePickup()
        {
            Pickup[] pickups = Object.FindObjectsByType<Pickup>();
            Assert.Greater(pickups.Length, 0, "The spawner placed no pickups after the bootstrap.");

            return pickups[0];
        }

        private static Pickup FindActivePickupAt(Vector3 point)
        {
            foreach (Pickup pickup in Object.FindObjectsByType<Pickup>())
            {
                if (Vector3.Distance(pickup.transform.position, point) < 0.01f) return pickup;
            }

            return null;
        }

        private static T FindSingle<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>();
            Assert.AreEqual(1, found.Length, $"Expected exactly one {typeof(T).Name}.");

            return found[0];
        }

        private static string HudText()
        {
            Label label = FindSingle<DebugHud>().GetComponent<UIDocument>().rootVisualElement.Q<Label>(DebugHud.LABEL_NAME);
            Assert.IsNotNull(label, "The HUD label is not in the UIDocument tree.");

            return label.text;
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
