using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GosipSimulator.Core.State;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Covers C2 and A2: the reference project kept the state enum and the state machine in two
    /// independent fields, so Exit never ran and the HUD reported a state the game was not in.
    /// Plain C#, no MonoBehaviour, which is the point of R5: these run in milliseconds.
    /// </summary>
    public class GameStateMachineTests
    {
        private static readonly Regex NullState = new Regex(@"^\[GameStateMachine\].*null state");

        private GameStateMachine _machine;
        private SpyState        _menu;
        private SpyState        _play;

        // ────────────────────────────────
        // TEST DOUBLES
        // ────────────────────────────────
        #region Test Doubles

        private sealed class SpyState : IGameState
        {
            public SpyState(GameState id) => Id = id;

            public GameState Id { get; }

            public int EnterCount { get; private set; }
            public int TickCount  { get; private set; }
            public int ExitCount  { get; private set; }

            public void Enter() => EnterCount++;
            public void Tick()  => TickCount++;
            public void Exit()  => ExitCount++;
        }

        #endregion

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            _machine = new GameStateMachine();
            _menu    = new SpyState(GameState.Menu);
            _play    = new SpyState(GameState.Play);
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void TransitionTo_CallsExitOnPrevious_AndEnterOnNew()
        {
            _machine.Initialize(_menu);

            _machine.TransitionTo(_play);

            Assert.AreEqual(1, _menu.ExitCount, "Exit was never called on the outgoing state (C2).");
            Assert.AreEqual(1, _play.EnterCount, "Enter was never called on the incoming state.");
            Assert.AreSame(_play, _machine.CurrentState);
        }

        [Test]
        public void TransitionTo_Null_KeepsCurrentState()
        {
            _machine.Initialize(_menu);

            LogAssert.Expect(LogType.Error, NullState);
            _machine.TransitionTo(null);

            Assert.AreSame(_menu, _machine.CurrentState, "An invalid transition corrupted the machine.");
            Assert.AreEqual(0, _menu.ExitCount, "Exit ran for a transition that never happened.");
        }

        [Test]
        public void Initialize_CallsEnterOnInitialState()
        {
            // Initialize had zero call sites in the reference project and was never proven to
            // work (A2, M11). This is that proof.
            _machine.Initialize(_menu);

            Assert.AreSame(_menu, _machine.CurrentState);
            Assert.AreEqual(1, _menu.EnterCount);
        }

        [Test]
        public void Initialize_Null_LeavesMachineUninitialized()
        {
            LogAssert.Expect(LogType.Error, NullState);
            _machine.Initialize(null);

            // Null reads back as GameState.Bootstrap through GameManager: a valid fallback
            // rather than a half-initialized machine.
            Assert.IsNull(_machine.CurrentState);
        }

        [Test]
        public void Tick_ForwardsToCurrentStateOnly()
        {
            _machine.Initialize(_menu);
            _machine.TransitionTo(_play);

            _machine.Tick();

            Assert.AreEqual(1, _play.TickCount);
            Assert.AreEqual(0, _menu.TickCount, "The outgoing state is still being ticked.");
        }

        [Test]
        public void Tick_BeforeInitialize_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _machine.Tick());
        }

        #endregion
    }
}
