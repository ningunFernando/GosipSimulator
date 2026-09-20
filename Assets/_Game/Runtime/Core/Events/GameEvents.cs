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

    // ────────────────────────────────
    // GOSSIP EVENTS
    // ────────────────────────────────
    #region Gossip Events

    /// <summary>
    /// Published by whoever owns perception, once per NPC that saw an action happen. Gossip turns it
    /// into an opinion and never learns how the witness was found, or that there was a crowd (R4).
    /// One event per witness rather than a list inside a single event, so a listener cannot miss part
    /// of a batch and the payload stays a struct that never allocates.
    /// </summary>
    public struct OnActionWitnessed
    {
        public string actionId;

        /// <summary>Who did it. The player, today; the field exists because an NPC could act too.</summary>
        public string actorId;

        /// <summary>Who saw it. This is whose opinion is about to change.</summary>
        public string witnessId;

        /// <summary>Who it was done to. Empty when the action had no victim.</summary>
        public string targetId;
    }

    /// <summary>
    /// Published by Gossip after an opinion actually changed. Save persists it, and anyone who has to
    /// react to how an NPC feels reads it; Gossip never learns that either happens (R4). Published
    /// only on a real change, the way GameManager publishes OnGameStateChanged, so a subscriber can
    /// treat every one of these as news instead of re-checking a value it already had.
    /// </summary>
    public struct OnRelationshipChanged
    {
        /// <summary>Whose opinion this is.</summary>
        public string npcId;

        /// <summary>Who the opinion is about.</summary>
        public string aboutId;

        public int previous;
        public int current;

        /// <summary>The action or the rumor that caused it, for the log and for the HUD.</summary>
        public string reason;
    }

    /// <summary>
    /// Published by Gossip for every hop a rumor travels along the social graph. One event per
    /// receiver, so a listener can show a story spreading without being handed the graph itself.
    /// </summary>
    public struct OnRumorSpread
    {
        public string actionId;

        /// <summary>Who passed it on.</summary>
        public string fromId;

        /// <summary>Who heard it. Gossip changes this receiver's own opinion separately.</summary>
        public string toId;

        /// <summary>Distance from the original witness, starting at 1. Zero is the sighting itself.</summary>
        public int hop;

        /// <summary>
        /// Magnitude still carried at this hop, after decay and after the trust between the two. A hop
        /// that decays to zero is not published at all, so every one of these moved somebody.
        /// </summary>
        public int weight;
    }

    /// <summary>
    /// Published by Save once the bootstrap is complete, carrying every opinion that was on disk.
    /// Gossip applies them over the graph it built from configuration, which is what keeps the two
    /// modules independent of handler order: Gossip never queries Save, and Save never references
    /// Gossip (R3). Empty arrays are a valid fresh save and not an error. Gossip must not publish
    /// OnRelationshipChanged while applying these, or loading a game would mark its own save dirty.
    /// </summary>
    public struct OnRelationshipsRestored
    {
        // Parallel arrays because the payload is a struct, and Save's own row type lives in the Save
        // assembly, which Core cannot see (R3).
        public string[] npcIds;
        public string[] aboutIds;
        public int[] values;
    }

    #endregion
}
