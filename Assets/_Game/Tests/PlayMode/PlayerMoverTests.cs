using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Player;
using GosipSimulator.Save;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Input to Rigidbody through the real scenes: a virtual keyboard holds W, and the Player placed
    /// in Scene_Game has to move, but only once the bootstrap has put the game in Play. Also the
    /// failure path: a mover missing its configuration disables itself with a clear error (R8, R9).
    /// </summary>
    public class PlayerMoverTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const string GameScene      = "Scene_Game";
        private const int    MaxFrames      = 600;
        private const float  HoldSeconds    = 0.5f;

        private static readonly Regex NoInputReader =
            new Regex(@"^\[PlayerMover\] PlayerInputReader not assigned");
        private static readonly Regex InvalidMovementValues =
            new Regex(@"^\[PlayerMover\] Invalid movement values");

        private Keyboard _keyboard;

#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode _previousBehavior;
#endif

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            // By default keyboard input only reaches the game while the Game view has focus, and a
            // test run rarely has it: the virtual key presses would be dropped without a trace.
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
        public IEnumerator HoldingW_InPlayState_MovesPlayerForwardAndStopsOnRelease()
        {
            SceneManager.LoadScene(BootstrapScene);
            yield return null;
            yield return WaitForPlay();

            Assert.IsTrue(GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Play,
                "The bootstrap never reached Play; see BootstrapSequenceTests.");

            Rigidbody body  = FindPlayerBody();
            Vector3   start = body.position;

            Hold(Key.W);
            yield return new WaitForSeconds(HoldSeconds);

            Assert.Greater(body.position.z - start.z, 0.5f, "Holding W did not move the player forward.");
            Assert.Greater(body.linearVelocity.z, 1f, "The player is not moving forward while W is held.");
            Assert.Less(Mathf.Abs(body.position.x - start.x), 0.05f, "W alone must not move the player sideways.");

            ReleaseAll();
            yield return new WaitForSeconds(HoldSeconds);

            Vector3 velocity = body.linearVelocity;
            Assert.Less(new Vector2(velocity.x, velocity.z).magnitude, 0.1f,
                "The player kept moving after W was released: canceled is not reaching the reader.");
        }

        [UnityTest]
        public IEnumerator HoldingW_WithoutBootstrap_DoesNotMovePlayer()
        {
            // No bootstrap means no OnGameStateChanged, so the reader never learns the game is in Play.
            SceneManager.LoadScene(GameScene);
            yield return null;

            Rigidbody body  = FindPlayerBody();
            Vector3   start = body.position;

            Hold(Key.W);
            yield return new WaitForSeconds(HoldSeconds);

            Assert.Less(Mathf.Abs(body.position.z - start.z), 0.05f,
                "Input moved the player although the game never reached Play.");

            ReleaseAll();
        }

        [Test]
        public void Awake_WithoutInputReader_LogsErrorAndDisables()
        {
            // Created inactive so Awake waits for SetActive, after the expectation is in place.
            var host = new GameObject("MoverWithoutInput");
            host.SetActive(false);
            PlayerMover mover = host.AddComponent<PlayerMover>();

            LogAssert.Expect(LogType.Error, NoInputReader);
            host.SetActive(true);

            Assert.IsFalse(mover.enabled, "A mover without input stayed enabled, so FixedUpdate would hit a null.");

            Object.Destroy(host);
        }

        [Test]
        public void Awake_NegativeMaxSpeed_LogsErrorAndDisables()
        {
            // OnValidate clamps this in the Inspector, but never runs in a build or for a value written
            // by code: Awake is the check that has to hold (R8). The reader host stays inactive, so its
            // own Awake never runs and never logs.
            var readerHost = new GameObject("UnusedReader");
            readerHost.SetActive(false);
            PlayerInputReader reader = readerHost.AddComponent<PlayerInputReader>();

            var host = new GameObject("MoverWithNegativeSpeed");
            host.SetActive(false);
            PlayerMover mover = host.AddComponent<PlayerMover>();
            SetField(mover, "_input", reader);
            SetField(mover, "_maxSpeed", -1f);

            LogAssert.Expect(LogType.Error, InvalidMovementValues);
            host.SetActive(true);

            Assert.IsFalse(mover.enabled, "A mover with a negative max speed stayed enabled.");

            Object.Destroy(host);
            Object.Destroy(readerHost);
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private void Hold(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
        }

        private void ReleaseAll()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
        }

        private static Rigidbody FindPlayerBody()
        {
            PlayerMover[] movers = Object.FindObjectsByType<PlayerMover>();
            Assert.AreEqual(1, movers.Length, "The game scene must hold exactly one PlayerMover.");

            return movers[0].GetComponent<Rigidbody>();
        }

        private static IEnumerator WaitForPlay()
        {
            int frames = 0;

            while (frames < MaxFrames &&
                   !(GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Play))
            {
                frames++;
                yield return null;
            }
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
