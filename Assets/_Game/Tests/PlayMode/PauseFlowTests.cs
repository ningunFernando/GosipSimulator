using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Debug;
using GosipSimulator.Player;
using GosipSimulator.Save;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The pause cycle through the real input path: Escape on a virtual keyboard reaches
    /// PlayerInputReader, travels the bus as OnPauseRequested, and GameManager toggles Play and Paused.
    /// Paused has to freeze scaled time and the player, and show on the HUD.
    /// </summary>
    [IsolatedSave]
    public class PauseFlowTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const int    MaxFrames      = 600;
        private const float  HoldSeconds    = 0.3f;

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
            // Keyboard input only reaches the game while the Game view has focus by default, and a test
            // run rarely has it (see PlayerMoverTests).
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

            // A test that fails while paused would otherwise freeze every later test: WaitForSeconds
            // and FixedUpdate both run on scaled time, and the destroyed GameManager never exits Paused.
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
        public IEnumerator PressingEscape_InPlay_PausesFreezesPlayerAndSecondPressResumes()
        {
            SceneManager.LoadScene(BootstrapScene);
            yield return null;
            yield return WaitForPlay();

            Assert.AreEqual(GameState.Play, CurrentState(), "The bootstrap never reached Play; see BootstrapSequenceTests.");

            yield return Tap(Key.Escape);

            Assert.AreEqual(GameState.Paused, CurrentState(), "Escape did not pause the game.");
            Assert.AreEqual(0f, Time.timeScale, "Paused did not freeze scaled time.");
            StringAssert.Contains("State: Paused", HudText(), "The HUD did not show the pause.");

            Rigidbody body  = FindPlayerBody();
            Vector3   start = body.position;

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            // Realtime on purpose: scaled time is stopped, so WaitForSeconds would never return.
            yield return new WaitForSecondsRealtime(HoldSeconds);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;

            Assert.Less(Vector3.Distance(start, body.position), 0.01f, "The player moved while the game was paused.");

            yield return Tap(Key.Escape);

            Assert.AreEqual(GameState.Play, CurrentState(), "A second Escape did not resume the game.");
            Assert.AreEqual(1f, Time.timeScale, "Resuming did not restore scaled time.");
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        /// <summary>Press and release across a few frames, so the action sees both edges.</summary>
        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            yield return null;

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static GameState CurrentState() =>
            GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Bootstrap;

        private static string HudText()
        {
            DebugHud[] huds = Object.FindObjectsByType<DebugHud>();
            Assert.AreEqual(1, huds.Length, "The game scene must hold exactly one DebugHud.");

            Label label = huds[0].GetComponent<UIDocument>().rootVisualElement.Q<Label>(DebugHud.LABEL_NAME);
            Assert.IsNotNull(label, "The HUD label is not in the UIDocument tree.");

            return label.text;
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

            while (frames < MaxFrames && CurrentState() != GameState.Play)
            {
                frames++;
                yield return null;
            }
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
