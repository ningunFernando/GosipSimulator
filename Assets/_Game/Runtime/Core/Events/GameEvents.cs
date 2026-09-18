using GosipSimulator.Core.State;

namespace GosipSimulator.Core
{
    // ────────────────────────────────
    // BOOTSTRAP EVENTS
    // ────────────────────────────────
    #region Bootstrap Events

    /// <summary>
    /// Published once, after the bootstrap sequence completes and the game scene is loaded.
    /// </summary>
    public struct OnBootstrapComplete
    {
    }

    #endregion

    // ────────────────────────────────
    // GAME STATE EVENTS
    // ────────────────────────────────
    #region Game State Events

    /// <summary>
    /// Published by GameManager after a transition that actually changed the state.
    /// </summary>
    public struct OnGameStateChanged
    {
        public GameState previousState;
        public GameState newState;
    }

    /// <summary>
    /// Published by the input owner when the player asks to pause or resume. Only a request:
    /// GameManager decides whether the current state allows it.
    /// </summary>
    public struct OnPauseRequested
    {
    }

    #endregion

    // ────────────────────────────────
    // PROGRESS EVENTS
    // ────────────────────────────────
    #region Progress Events

    /// <summary>
    /// Published by ProgressService after any mutation of the saved progress.
    /// </summary>
    public struct OnProgressChanged
    {
        public int currency;
        public int totalEarned;
    }

    #endregion

    // ────────────────────────────────
    // PICKUP EVENTS
    // ────────────────────────────────
    #region Pickup Events

    /// <summary>
    /// Published by PickupSpawner when the player collects a pickup. Save turns it into currency;
    /// the Pickups module never learns what the value is used for (R4).
    /// </summary>
    public struct OnPickupCollected
    {
        public int value;
    }

    #endregion
}
