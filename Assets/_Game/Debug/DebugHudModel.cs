using System;
using System.Collections.Generic;
using System.Text;
using GosipSimulator.Core.State;

namespace GosipSimulator.Debug
{
    /// <summary>
    /// What the HUD displays, kept out of the MonoBehaviour so EditMode can test it without a
    /// panel, a scene or Play Mode (R5).
    ///
    /// Everything here is a copy built from events, never a reference into the module that owns
    /// it: opinions are Gossip's, terms are Shop's, currency is Save's (R7). The HUD is the one place
    /// that sees all three at once, which is why it is a debug tool and not a gameplay module.
    /// </summary>
    public class DebugHudModel
    {
        /// <summary>How many rumor hops the HUD keeps. Older ones scroll off.</summary>
        public const int RUMOR_LINES = 3;

        private bool       _bootstrapComplete;

        // Nullable on purpose: GameState.Bootstrap is a real value GameManager reports, so it
        // cannot double as "no state change received yet". Currency follows the same rule, since
        // zero is a real balance.
        private GameState? _state;
        private int?       _currency;

        // Sorted with an ordinal comparer so the text is the same on every run and every machine.
        // Arrival order would reshuffle the list each time a rumor landed somewhere new.
        private readonly SortedDictionary<string, OpinionLine> _opinions
            = new SortedDictionary<string, OpinionLine>(StringComparer.Ordinal);

        private readonly SortedDictionary<string, string> _shops
            = new SortedDictionary<string, string>(StringComparer.Ordinal);

        private readonly Queue<string> _rumors = new Queue<string>(RUMOR_LINES);

        private string _lastPurchase;

        private struct OpinionLine
        {
            public string npcId;
            public string aboutId;
            public int    value;
            public string reason;
        }

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public string Text
        {
            get
            {
                var text = new StringBuilder();

                text.Append("Bootstrap: ").Append(_bootstrapComplete ? "complete" : "pending").Append('\n');
                text.Append("State: ").Append(_state.HasValue ? _state.Value.ToString() : "unknown").Append('\n');
                text.Append("Currency: ").Append(_currency.HasValue ? _currency.Value.ToString() : "unknown");

                text.Append("\n\nOpinions:");

                if (_opinions.Count == 0) text.Append(" none");

                foreach (OpinionLine line in _opinions.Values)
                {
                    text.Append("\n  ").Append(line.npcId).Append(" -> ").Append(line.aboutId)
                        .Append(": ").Append(Signed(line.value));

                    if (!string.IsNullOrEmpty(line.reason)) text.Append(" (").Append(line.reason).Append(')');
                }

                text.Append("\n\nRumors:");

                if (_rumors.Count == 0) text.Append(" none");

                foreach (string rumor in _rumors) text.Append("\n  ").Append(rumor);

                text.Append("\n\nShop:");

                if (_shops.Count == 0) text.Append(" unknown");

                foreach (KeyValuePair<string, string> shop in _shops)
                {
                    text.Append("\n  ").Append(shop.Key).Append(": ").Append(shop.Value);
                }

                text.Append("\nLast purchase: ").Append(_lastPurchase ?? "none");

                return text.ToString();
            }
        }

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

        /// <summary>
        /// One opinion, as OnRelationshipChanged reports it. Zero removes the line, the same sparse
        /// rule Gossip and Save follow, so the HUD never lists somebody who has gone back to neutral.
        /// </summary>
        public void SetOpinion(string npcId, string aboutId, int value, string reason)
        {
            // A malformed event would otherwise render as " -> : -5". Dropped here because the
            // modules that publish it already reject empty ids and say so.
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(aboutId)) return;

            string key = npcId + "\n" + aboutId;

            if (value == 0)
            {
                _opinions.Remove(key);
                return;
            }

            _opinions[key] = new OpinionLine { npcId = npcId, aboutId = aboutId, value = value, reason = reason };
        }

        /// <summary>
        /// What Save read off the disk. Applied over what the HUD already shows rather than
        /// replacing it, which is what Gossip does with the same payload. The file does not carry
        /// reasons through the event, so restored lines are marked as saved instead.
        /// </summary>
        public void RestoreOpinions(string[] npcIds, string[] aboutIds, int[] values)
        {
            int count = npcIds?.Length ?? 0;

            // Gossip throws on mismatched arrays and logs why; the HUD shows nothing new instead of
            // pairing the wrong npc with the wrong value.
            if ((aboutIds?.Length ?? 0) != count || (values?.Length ?? 0) != count) return;

            for (int i = 0; i < count; i++) SetOpinion(npcIds[i], aboutIds[i], values[i], "saved");
        }

        /// <summary>One hop of a story travelling. The oldest line drops off past RUMOR_LINES.</summary>
        public void AddRumor(string actionId, string fromId, string toId, int hop, int weight)
        {
            if (_rumors.Count == RUMOR_LINES) _rumors.Dequeue();

            _rumors.Enqueue($"{fromId} -> {toId}: {actionId}, hop {hop}, {Signed(weight)}");
        }

        public void SetShopTerms(string shopkeeperId, int price, int percent, bool refuses)
        {
            if (string.IsNullOrEmpty(shopkeeperId)) return;

            _shops[shopkeeperId] = refuses
                ? $"refuses to sell (would be {price} at {percent}%)"
                : $"{price} ({percent}%)";
        }

        public void RecordPurchaseSettled(string shopkeeperId, string itemId, int price, bool paid)
        {
            _lastPurchase = paid
                ? $"bought {itemId} from {shopkeeperId} for {price}"
                : $"could not afford {itemId} at {price}";
        }

        public void RecordPurchaseRefused(string shopkeeperId, string itemId, int opinion)
        {
            _lastPurchase = $"{shopkeeperId} refused to sell {itemId} (opinion {Signed(opinion)})";
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        // An explicit plus, so a good opinion and a bad one read differently at a glance and a
        // rumor of +3 is not mistaken for a hop count.
        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

        #endregion
    }
}
