using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;
using GosipSimulator.Core.State;
using GosipSimulator.Debug;
using GosipSimulator.Save;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The one thing EditMode cannot prove: that the scene, the prefabs and the Inspector wiring
    /// actually produce a running game. Everything else about the bootstrap is checked in
    /// milliseconds elsewhere; this is the end-to-end smoke test the Definition of Done asks for.
    /// </summary>
    public class BootstrapSequenceTests
    {
        private const string BootstrapScene = "Scene_Bootstrap";
        private const string GameScene      = "Scene_Game";

        private static readonly Regex NotFromBootstrap =
            new Regex(@"^\[Bootstrapper\] Play Mode started in 'Scene_Game'");

        /// <summary>
        /// Generous on purpose. The sequence yields three frames and then loads a scene, so the
        /// real cost is the scene load; this is a deadlock guard, not a performance budget.
        /// </summary>
        private const int MaxFrames = 600;

        // ────────────────────────────────
        // TEARDOWN
        // ────────────────────────────────
        #region Teardown

        /// <summary>
        /// The managers live in DontDestroyOnLoad, so without this they outlive the test and the
        /// next run starts with a GameManager already claiming the singleton.
        /// </summary>
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

        [UnityTest]
        public IEnumerator Bootstrap_FromBootstrapScene_ReachesGameSceneInPlayState()
        {
            yield return BootAndWaitForPlay();

            Assert.IsNotNull(GameManager.Instance,
                $"No GameManager after {MaxFrames} frames. The sequence never instantiated the managers.");

            // Play is only reached through StartGame, which only runs from the sceneLoaded
            // handler. Asserting it covers the whole chain, including the handler that must not
            // be unsubscribed in OnDestroy.
            Assert.AreEqual(GameState.Play, GameManager.Instance.CurrentState,
                "The bootstrap finished but the game never started.");

            Assert.AreNotEqual(BootstrapScene, SceneManager.GetActiveScene().name,
                "The game scene was never loaded.");

            // Unity's null check, not NUnit's: a destroyed manager is still a non-null C# reference, and
            // IsNotNull kept passing while the pool was destroyed together with Scene_Bootstrap.
            Assert.IsTrue(GameManager.Instance.PoolManager != null,
                "The pool manager was never injected (R6), or it was destroyed with the bootstrap scene.");

            Assert.AreEqual(1, Object.FindObjectsByType<SaveSystem>().Length,
                "The save system did not survive the load of the game scene.");
        }

        [UnityTest]
        public IEnumerator Bootstrap_FromBootstrapScene_DebugHudShowsCompletedSequence()
        {
            yield return BootAndWaitForPlay();

            Assert.IsTrue(HasStarted(), "The game never reached Play, so the HUD has nothing to show.");

            DebugHud[] huds = Object.FindObjectsByType<DebugHud>();
            Assert.AreEqual(1, huds.Length, "The game scene must hold exactly one DebugHud.");

            Label label = huds[0].GetComponent<UIDocument>().rootVisualElement.Q<Label>(DebugHud.LABEL_NAME);

            // Missing means UIDocument rebuilt its root after DebugHud.OnEnable added the label.
            Assert.IsNotNull(label, "The HUD label is not in the UIDocument tree.");

            // Both events are published from the sceneLoaded handler, while the HUD is a scene
            // object that subscribed in OnEnable during that same load (R10).
            StringAssert.Contains("Bootstrap: complete", label.text, "OnBootstrapComplete never reached the HUD.");
            StringAssert.Contains("State: Play", label.text, "OnGameStateChanged never reached the HUD.");
        }

        [UnityTest]
        public IEnumerator EntryGuard_FromGameScene_LogsClearError()
        {
            SceneManager.LoadScene(GameScene);
            yield return null;

            LogAssert.Expect(LogType.Error, NotFromBootstrap);
            InvokeEntryGuard();

            // A few frames with no bootstrap behind the scene. Any NullReferenceException fails
            // the test as an unexpected error: the "degrades, does not explode" half of the
            // Definition of Done.
            for (int i = 0; i < 5; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator EntryGuard_FromBootstrapScene_LogsNothing()
        {
            SceneManager.LoadScene(BootstrapScene);
            yield return null;

            // Any error logged by the guard fails the test as unexpected.
            InvokeEntryGuard();

            // Let the sequence finish, so no bootstrap coroutine is left running into the next test.
            yield return WaitForPlay();
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

            yield return WaitForPlay();
        }

        private static IEnumerator WaitForPlay()
        {
            int frames = 0;

            while (frames < MaxFrames && !HasStarted())
            {
                frames++;
                yield return null;
            }
        }

        private static bool HasStarted() =>
            GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Play;

        /// <summary>
        /// Unity calls the guard once per Play Mode session, before any test runs, so the tests
        /// call it by hand. Checking the attribute is what proves Unity will call it for real.
        /// </summary>
        private static void InvokeEntryGuard()
        {
            MethodInfo method = typeof(Bootstrapper)
                .GetMethod("CheckEntryScene", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method,
                "Bootstrapper.CheckEntryScene was renamed or removed. Update this test seam.");

            var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

            Assert.IsNotNull(attribute,
                "CheckEntryScene lost [RuntimeInitializeOnLoadMethod], so Unity never runs it.");
            Assert.AreEqual(RuntimeInitializeLoadType.AfterSceneLoad, attribute.loadType);

            method.Invoke(null, null);
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
