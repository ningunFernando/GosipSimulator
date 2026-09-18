using GosipSimulator.Core.State;

namespace GosipSimulator.Debug
{
    /// <summary>
    /// What the HUD displays, kept out of the MonoBehaviour so EditMode can test it without a
    /// panel, a scene or Play Mode (R5).
    /// </summary>
    public class DebugHudModel
    {
        private bool       _bootstrapComplete;

        // Nullable on purpose: GameState.Bootstrap is a real value GameManager reports, so it
        // cannot double as "no state change received yet". Currency follows the same rule, since
        // zero is a real balance.
        private GameState? _state;
        private int?       _currency;

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public string Text =>
            $"Bootstrap: {(_bootstrapComplete ? "complete" : "pending")}\n" +
            $"State: {(_state.HasValue ? _state.Value.ToString() : "unknown")}\n" +
            $"Currency: {(_currency.HasValue ? _currency.Value.ToString() : "unknown")}";

        public void MarkBootstrapComplete()
        {
            _bootstrapComplete = true;
        }

        public void SetState(GameState state)
        {
            _state = state;
        }

        public void SetCurrency(int currency)
        {
            _currency = currency;
        }

        #endregion
    }
}
