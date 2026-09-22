using UnityEngine;
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
    // ACTION EVENTS
    // ────────────────────────────────
    #region Action Events

    /// <summary>
    /// Published by whoever owns the player's verbs, once, when an action actually happens. It says
    /// what was done and where, and nothing about who noticed: working out who was looking belongs to
    /// perception, and the publisher never learns the answer (R4).
    ///
    /// The position is what perception needs and the only reason it is here. An id would not do: two
    /// NPCs standing in different places have to get different answers about the same action.
    /// </summary>
    public struct OnActionCommitted
    {
        public string actionId;

        /// <summary>Who did it. The player, today; an NPC could act too.</summary>
        public string actorId;

        /// <summary>Who it was done to. Empty when the action had no victim.</summary>
        public string targetId;

        /// <summary>Where it happened, in world space.</summary>
        public Vector3 position;
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

    // ────────────────────────────────
    // SHOP EVENTS
    // ────────────────────────────────
    #region Shop Events

    /// <summary>
    /// Published by a shopkeeper whenever what it would charge a customer changed, and once at every
    /// restore so a listener starts from the real terms instead of guessing the base price. It is the
    /// shop's own reading of an opinion: the multiplier belongs to Shop, the opinion to Gossip (R7).
    /// </summary>
    public struct OnShopTermsChanged
    {
        /// <summary>The NPC who runs the shop.</summary>
        public string shopkeeperId;

        /// <summary>Who these terms are for.</summary>
        public string customerId;

        /// <summary>The shopkeeper's opinion of the customer that produced these terms.</summary>
        public int opinion;

        /// <summary>The price as a percentage of the base price. 100 is the base price.</summary>
        public int pricePercent;

        /// <summary>What the item costs right now, in currency.</summary>
        public int price;

        /// <summary>True when the shopkeeper will not sell at any price.</summary>
        public bool refuses;
    }

    /// <summary>
    /// Published by a shopkeeper that agreed to sell. It is an agreement, not a sale: whether the
    /// customer can pay belongs to whoever owns the currency, which answers with OnPurchaseSettled.
    /// Shop never learns what the balance is, and Save never learns why the price is what it is (R4).
    /// </summary>
    public struct OnPurchaseApproved
    {
        public string shopkeeperId;
        public string customerId;
        public string itemId;

        /// <summary>Always one or more. A shop cannot approve giving something away.</summary>
        public int price;
    }

    /// <summary>
    /// Published by a shopkeeper that will not sell to this customer at all. Nothing is spent and
    /// nothing reaches Save. The opinion is carried so a listener can say why without asking Gossip.
    /// </summary>
    public struct OnPurchaseRefused
    {
        public string shopkeeperId;
        public string customerId;
        public string itemId;
        public int opinion;
    }

    /// <summary>
    /// Published by the owner of the currency once it has tried to take the price of an approved
    /// purchase. Paid or not, this is the end of that purchase, which is why a failed payment is an
    /// event rather than silence: without it the player would press the key and see nothing (R9).
    /// </summary>
    public struct OnPurchaseSettled
    {
        public string shopkeeperId;
        public string customerId;
        public string itemId;
        public int price;

        /// <summary>False when the customer could not afford it. The balance was left untouched.</summary>
        public bool paid;
    }

    #endregion
}
