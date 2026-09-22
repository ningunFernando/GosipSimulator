using System;
using UnityEngine;
using GosipSimulator.Core;
using GosipSimulator.Data;

namespace GosipSimulator.Shop
{
    /// <summary>
    /// The adapter of the module: somebody who sells one thing and reacts to what they have heard.
    /// The rule lives in PricingPolicy; this class only keeps its copy of one opinion up to date and
    /// answers when the customer asks to buy (R5).
    ///
    /// Shop cannot ask Gossip what the shopkeeper thinks (R3), so the opinion is pushed: every
    /// OnRelationshipChanged about this pair updates the cached value, and OnRelationshipsRestored
    /// seeds it at boot, because restoring is silent and would otherwise leave a loaded grudge
    /// invisible until the next rumor. The opinion stays Gossip's; the price is this class's (R7).
    ///
    /// A purchase is asked for with an ordinary action, the same OnActionCommitted a robbery is, so
    /// the counter is just another Interactable and Shop never references Actions (R4). Whether the
    /// customer can pay is not decided here: approving hands that to Save, which owns the currency.
    /// </summary>
    public class Shopkeeper : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Shopkeeper")]
        [Tooltip("Who runs this shop. The price follows this NPC's opinion of the customer.")]
        [SerializeField] private NpcDefinitionSO _shopkeeper;

        [Tooltip("The verb that asks to buy. The same asset the counter's Interactable carries.")]
        [SerializeField] private ActionDefinitionSO _tradeAction;

        [Header("Customer")]
        [Tooltip("Who this shop serves. Must match the actor id the player acts with.")]
        [SerializeField] private string _customerId = "player";

        [Header("Stock")]
        [Tooltip("What is sold, as an id. Written into every purchase event.")]
        [SerializeField] private string _itemId = "horseshoe";

        [Tooltip("What a stranger pays, with an opinion of zero.")]
        [SerializeField] private int _basePrice = 5;

        [Header("Pricing")]
        [Tooltip("Percentage the price moves per point of opinion. At 2, an opinion of -5 costs 110%.")]
        [SerializeField] private int _percentPerPoint = 2;

        [Tooltip("The best discount anybody gets, as a percentage of the base price.")]
        [SerializeField] private int _minPercent = 50;

        [Tooltip("The opinion at which the shopkeeper stops selling altogether.")]
        [SerializeField] private int _refuseAtOrBelow = -30;

        #endregion

        private PricingPolicy _policy;
        private ShopTerms     _terms;
        private int           _opinion;
        private bool          _isSubscribed;

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>The id of the NPC who runs the shop, or null when none is assigned.</summary>
        public string ShopkeeperId => _shopkeeper != null ? _shopkeeper.Id : null;

        /// <summary>The cached opinion the terms were computed from. For the tests.</summary>
        public int Opinion => _opinion;

        /// <summary>What the customer is offered right now. For the tests.</summary>
        public ShopTerms Terms => _terms;

        #endregion

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void OnValidate()
        {
            // The Inspector accepts any int. These are the values PricingPolicy would reject in Awake,
            // pulled back into range while editing so the mistake never reaches Play (R8).
            _basePrice       = Mathf.Max(1, _basePrice);
            _percentPerPoint = Mathf.Max(0, _percentPerPoint);
            _minPercent      = Mathf.Clamp(_minPercent, 1, 100);
        }

        private void Awake()
        {
            // Setting enabled makes Unity call OnDisable on the spot, before any OnEnable ran, which
            // is why OnDisable only undoes what _isSubscribed says actually happened (R9).
            if (!IsConfigured(out string problem))
            {
                Log.Error($"[Shopkeeper] {problem} The shop is closed.");
                enabled = false;
                return;
            }

            try
            {
                _policy = new PricingPolicy(_basePrice, _percentPerPoint, _minPercent, _refuseAtOrBelow);
            }
            catch (ArgumentException e)
            {
                // OnValidate never runs in a build, so a bad value can still arrive here (R8).
                Log.Error($"[Shopkeeper] The pricing was rejected: {e.Message} The shop is closed.");
                enabled = false;
                return;
            }

            _opinion = 0;
            _terms   = _policy.For(_opinion);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<OnRelationshipChanged>(HandleRelationshipChanged);
            EventBus.Subscribe<OnRelationshipsRestored>(HandleRelationshipsRestored);
            EventBus.Subscribe<OnActionCommitted>(HandleActionCommitted);

            _isSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_isSubscribed) return;

            EventBus.Unsubscribe<OnRelationshipChanged>(HandleRelationshipChanged);
            EventBus.Unsubscribe<OnRelationshipsRestored>(HandleRelationshipsRestored);
            EventBus.Unsubscribe<OnActionCommitted>(HandleActionCommitted);

            _isSubscribed = false;
        }

        #endregion

        // ────────────────────────────────
        // EVENT HANDLERS
        // ────────────────────────────────
        #region Event Handlers

        private void HandleRelationshipChanged(OnRelationshipChanged e)
        {
            if (!IsOurPair(e.npcId, e.aboutId)) return;

            SetOpinion(e.current);
        }

        private void HandleRelationshipsRestored(OnRelationshipsRestored e)
        {
            int count = e.npcIds?.Length ?? 0;

            if ((e.aboutIds?.Length ?? 0) != count || (e.values?.Length ?? 0) != count)
            {
                // Gossip throws on the same payload and says so. Here the rows are ignored rather
                // than half read, and the terms still go out so the HUD is not left blank.
                Log.Error("[Shopkeeper] The restored opinions had arrays of different lengths. " +
                          "The saved opinion was not applied to the price.");
            }
            else
            {
                // Applied over the current value rather than resetting it, the same way Gossip's
                // Restore treats a pair the file does not mention. The two must agree, or the shop
                // would charge from a different opinion than the one the village holds.
                for (int i = 0; i < count; i++)
                {
                    if (IsOurPair(e.npcIds[i], e.aboutIds[i])) _opinion = e.values[i];
                }
            }

            _terms = _policy.For(_opinion);

            // Always, even when nothing changed: this is the moment a listener learns the starting
            // terms, the way Save publishes an empty restore instead of staying silent.
            PublishTerms();
        }

        private void HandleActionCommitted(OnActionCommitted e)
        {
            if (!string.Equals(e.actionId, _tradeAction.Id, StringComparison.Ordinal)) return;

            // Somebody else's counter. Not an error: a second shop would receive the same action.
            if (!string.Equals(e.targetId, _shopkeeper.Id, StringComparison.Ordinal)) return;

            if (!string.Equals(e.actorId, _customerId, StringComparison.Ordinal))
            {
                // Only the player trades today. Reported rather than ignored, because an NPC that
                // can act and silently cannot buy would look like a broken counter (R9).
                Log.Warn($"[Shopkeeper] '{e.actorId}' tried to buy from '{_shopkeeper.Id}', who only " +
                         $"serves '{_customerId}'. Nothing was sold.");
                return;
            }

            if (_terms.Refuses)
            {
                EventBus.Publish(new OnPurchaseRefused
                {
                    shopkeeperId = _shopkeeper.Id,
                    customerId   = _customerId,
                    itemId       = _itemId,
                    opinion      = _opinion
                });

                Log.Trace($"[Shopkeeper] '{_shopkeeper.Id}' refused to sell to '{_customerId}' at {_opinion}.");
                return;
            }

            EventBus.Publish(new OnPurchaseApproved
            {
                shopkeeperId = _shopkeeper.Id,
                customerId   = _customerId,
                itemId       = _itemId,
                price        = _terms.Price
            });

            Log.Trace($"[Shopkeeper] '{_shopkeeper.Id}' agreed to sell '{_itemId}' for {_terms}.");
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private bool IsOurPair(string npcId, string aboutId) =>
            string.Equals(npcId, _shopkeeper.Id, StringComparison.Ordinal) &&
            string.Equals(aboutId, _customerId, StringComparison.Ordinal);

        private void SetOpinion(int opinion)
        {
            // Gossip only publishes real changes, so this is always news. The terms are recomputed
            // and published even when the floor clamps them to the same price, because the opinion
            // inside the event did change and a listener showing it would otherwise go stale.
            _opinion = opinion;
            _terms   = _policy.For(_opinion);

            PublishTerms();
        }

        private void PublishTerms()
        {
            EventBus.Publish(new OnShopTermsChanged
            {
                shopkeeperId = _shopkeeper.Id,
                customerId   = _customerId,
                opinion      = _opinion,
                pricePercent = _terms.Percent,
                price        = _terms.Price,
                refuses      = _terms.Refuses
            });
        }

        private bool IsConfigured(out string problem)
        {
            problem = null;

            if (_shopkeeper == null)
            {
                problem = "No shopkeeper NPC assigned.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_shopkeeper.Id))
            {
                problem = $"The shopkeeper '{_shopkeeper.name}' has an empty id.";
                return false;
            }

            if (_tradeAction == null)
            {
                problem = "No trade action assigned, so nothing can ask to buy.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_tradeAction.Id))
            {
                problem = $"The trade action '{_tradeAction.name}' has an empty id.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_customerId))
            {
                problem = "Customer id is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_itemId))
            {
                // Save does not use it, but every purchase event carries it and the HUD names the
                // item with it. An empty one would read as a sale of nothing.
                problem = "Item id is empty.";
                return false;
            }

            return true;
        }

        #endregion
    }
}
