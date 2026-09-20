using System;
using System.Collections.Generic;
using GosipSimulator.Core;

namespace GosipSimulator.Save
{
    /// <summary>
    /// The domain layer for the persisted opinions, the counterpart of ProgressService: it owns the
    /// rules for what reaches the file and leaves the Unity lifecycle to SaveSystem (R5). Plain C#,
    /// so every rule here runs in an EditMode test in milliseconds.
    ///
    /// This is a copy of what Gossip owns, not a second source of truth. Save cannot reference
    /// Gossip (R3), so it learns about every change from OnRelationshipChanged and keeps its own
    /// row, exactly the way SaveSystem already turns OnPickupCollected into currency without
    /// Pickups knowing (R4, R7).
    /// </summary>
    public class RelationshipStore
    {
        private readonly SaveData _data;

        // ────────────────────────────────
        // CONSTRUCTOR
        // ────────────────────────────────
        #region Constructor

        public RelationshipStore(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            // A v1 file migrated forward, or a SaveData built by hand, can arrive with no list at
            // all. Repairing it once here beats a null check at every call site.
            data.relationships ??= new List<RelationshipRow>();

            _data = data;
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>How many opinions are on file. Zero for a village nobody has wronged.</summary>
        public int Count => _data.relationships.Count;

        /// <summary>The stored opinion, or zero when this pair has no row, matching RelationshipGraph.</summary>
        public int Get(string npcId, string aboutId)
        {
            int index = IndexOf(npcId, aboutId);

            return index < 0 ? 0 : _data.relationships[index].value;
        }

        /// <summary>
        /// Writes what an NPC now thinks of somebody. Returns false when the call was rejected, which
        /// is only ever malformed ids: a bad event should cost one row, not the whole save (R9).
        /// </summary>
        public bool Record(string npcId, string aboutId, int value, string reason)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(aboutId)) return false;

            // Self-opinions are rejected by RelationshipGraph, so one arriving here means the event
            // did not come from Gossip. Persisting it would put a row on disk that the graph would
            // refuse to load back.
            if (string.Equals(npcId, aboutId, StringComparison.Ordinal)) return false;

            int index = IndexOf(npcId, aboutId);

            // Sparse, like the graph it mirrors: an opinion back at zero is indistinguishable from
            // one that never existed, so keeping the row would grow the file forever with nothing.
            if (value == 0)
            {
                if (index >= 0) _data.relationships.RemoveAt(index);
                return true;
            }

            if (index >= 0)
            {
                _data.relationships[index].value  = value;
                _data.relationships[index].reason = reason;
                return true;
            }

            _data.relationships.Add(new RelationshipRow
            {
                npcId   = npcId,
                aboutId = aboutId,
                value   = value,
                reason  = reason
            });

            return true;
        }

        /// <summary>
        /// The whole table as the event Gossip expects. Built here rather than in the adapter so the
        /// shape of the payload is covered by an EditMode test; SaveSystem only decides when to
        /// publish it. Empty arrays are what a fresh save produces and are not an error.
        /// </summary>
        public OnRelationshipsRestored ToRestoredPayload()
        {
            int count = _data.relationships.Count;

            var npcIds   = new string[count];
            var aboutIds = new string[count];
            var values   = new int[count];

            for (int i = 0; i < count; i++)
            {
                npcIds[i]   = _data.relationships[i].npcId;
                aboutIds[i] = _data.relationships[i].aboutId;
                values[i]   = _data.relationships[i].value;
            }

            return new OnRelationshipsRestored
            {
                npcIds   = npcIds,
                aboutIds = aboutIds,
                values   = values
            };
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private int IndexOf(string npcId, string aboutId)
        {
            // A linear scan on purpose. The village is four NPCs and the list is sparse, so a
            // dictionary beside the list would be a second structure to keep in step with the one
            // JsonUtility actually serializes (R7).
            for (int i = 0; i < _data.relationships.Count; i++)
            {
                RelationshipRow row = _data.relationships[i];

                if (string.Equals(row.npcId, npcId, StringComparison.Ordinal)
                    && string.Equals(row.aboutId, aboutId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion
    }
}
