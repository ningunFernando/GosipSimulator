namespace GosipSimulator.Core.State
{
    /// <summary>
    /// Owns the current IGameState and is the single source of truth for it (R7): GameManager
    /// derives its enum from here instead of keeping a second field that can diverge (C2, A2).
    /// </summary>
    public class GameStateMachine
    {
        public IGameState CurrentState { get; private set; }

        // ────────────────────────────────
        // INITIALIZATION
        // ────────────────────────────────
        #region Initialization

        public void Initialize(IGameState initialState)
        {
            if (initialState == null)
            {
                // CurrentState stays null, which reads back as GameState.Bootstrap: a valid
                // fallback rather than a half-initialized machine.
                Log.Error("[GameStateMachine] Initialize called with a null state.");
                return;
            }

            CurrentState = initialState;
            CurrentState.Enter();

            Log.Trace($"[GameStateMachine] Initial state: {initialState.Id}");
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public void TransitionTo(IGameState newState)
        {
            if (newState == null)
            {
                Log.Error("[GameStateMachine] TransitionTo called with a null state. Keeping the current one.");
                return;
            }

            string previous = CurrentState != null ? CurrentState.Id.ToString() : "none";

            Log.Trace($"[GameStateMachine] {previous} -> {newState.Id}");

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        public void Tick()
        {
            CurrentState?.Tick();
        }

        #endregion
    }
}
