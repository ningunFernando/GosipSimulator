using System;
using System.Collections.Generic;

namespace GosipSimulator.Gossip
{
    /// <summary>
    /// What each NPC thinks of somebody else, as a directed signed value. Plain C#, with no
    /// UnityEngine and no EventBus, so every rule here runs in an EditMode test in milliseconds (R5)
    /// and GossipService stays the only thing in this module allowed to publish (R4).
    ///
    /// Directed on purpose. The blacksmith distrusting the player says nothing about what the player
    /// thinks of the blacksmith, and a rumor walking from the son to the father has to be able to move
    /// one edge without moving its reverse.
    ///
    /// This is the single source of truth for opinions (R7): whoever needs one either owns this graph
    /// or hears about a change on the bus. Nobody keeps a second copy, which is exactly how the
    /// reference project ended up with a state field that nothing read.
    ///
    /// Zero means "no opinion", and an unknown pair reads as zero rather than throwing. That is what
    /// lets the graph stay sparse: only NPCs whose opinion actually moved are ever stored.
    /// </summary>
    public class RelationshipGraph
    {
        private readonly Dictionary<(string npc, string about), int> _opinions
            = new Dictionary<(string npc, string about), int>();

        private readonly int _minValue;
        private readonly int _maxValue;

        // ────────────────────────────────
        // CONSTRUCTOR
        // ────────────────────────────────
        #region Constructor

        public RelationshipGraph(int minValue, int maxValue)
        {
            // Throwing rather than swapping the two: an inverted range is a configuration bug, and
            // quietly repairing it would hide a GossipConfigSO authored as min 100 max -100 (R9).
            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(minValue), minValue,
                    $"Must be lower than maxValue, which is {maxValue}.");
            }

            // Zero has to be inside the range, because an unknown pair reads as zero and the first
            // delta is applied on top of that reading. A range excluding zero would clamp a brand new
            // opinion the moment it was created, and every NPC would start at the same extreme.
            if (minValue > 0 || maxValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minValue), minValue,
                    $"Range [{minValue}, {maxValue}] must contain zero, which is what an unknown pair reads as.");
            }

            _minValue = minValue;
            _maxValue = maxValue;
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public int MinValue => _minValue;

        public int MaxValue => _maxValue;

        /// <summary>
        /// How many opinions are actually stored, which is not how many NPCs exist. A pair nobody has
        /// moved is absent, and that is what keeps the save from growing one row per NPC the player
        /// merely walked past.
        /// </summary>
        public int Count => _opinions.Count;

        /// <summary>
        /// Read-only view for the save to enumerate. Exposed as a read-only dictionary so a caller
        /// cannot mutate the graph behind GossipService's back and skip the event that goes with it.
        /// </summary>
        public IReadOnlyDictionary<(string npc, string about), int> All => _opinions;

        /// <summary>The clamped opinion of npcId about aboutId, or zero if nothing ever moved it.</summary>
        public int Get(string npcId, string aboutId)
        {
            ValidatePair(npcId, aboutId);

            return _opinions.TryGetValue(Key(npcId, aboutId), out int value) ? value : 0;
        }

        /// <summary>
        /// Moves an opinion by delta and returns the clamped result. A zero delta never creates an
        /// entry: neutral is already what an unknown pair reads as, so materialising it would add a
        /// row for every interaction that meant nothing and dirty the save for no change at all.
        /// </summary>
        public int Apply(string npcId, string aboutId, int delta)
        {
            ValidatePair(npcId, aboutId);

            (string npc, string about) key = Key(npcId, aboutId);

            int current = _opinions.TryGetValue(key, out int stored) ? stored : 0;

            if (delta == 0) return current;

            // Widened to long before adding: a delta of int.MaxValue against an opinion already near
            // the top of the range would wrap negative and clamp to the wrong end.
            int updated = Clamp((long)current + delta);

            _opinions[key] = updated;

            return updated;
        }

        /// <summary>
        /// Writes an opinion outright, clamped, and publishes nothing. This is the restore path: Save
        /// hands back whatever was on disk, and a hand-edited or half-corrupted file must not throw
        /// while a game is loading (R9). Silent by design, because applying a loaded save through
        /// Apply would publish one OnRelationshipChanged per row and mark the save dirty the instant
        /// it finished loading.
        /// </summary>
        public void Set(string npcId, string aboutId, int value)
        {
            ValidatePair(npcId, aboutId);

            (string npc, string about) key = Key(npcId, aboutId);

            int clamped = Clamp(value);

            // The same rule as Apply, so Set and Apply cannot disagree about what Count means. Remove
            // on an absent key is a no-op, which is what makes setting an existing opinion back to
            // neutral drop its row instead of storing a zero.
            if (clamped == 0)
            {
                _opinions.Remove(key);
                return;
            }

            _opinions[key] = clamped;
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private static (string npc, string about) Key(string npcId, string aboutId)
        {
            return (npcId, aboutId);
        }

        private int Clamp(long value)
        {
            if (value < _minValue) return _minValue;
            if (value > _maxValue) return _maxValue;

            return (int)value;
        }

        /// <summary>
        /// Ids come from ScriptableObjects, so an empty one is an authoring mistake inside an asset.
        /// Failing here names the field; accepting it would file an opinion under "" where no lookup
        /// will ever think to look, and the rumor would appear to do nothing.
        /// </summary>
        private static void ValidatePair(string npcId, string aboutId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                throw new ArgumentException("An id cannot be null, empty or whitespace.", nameof(npcId));
            }

            if (string.IsNullOrWhiteSpace(aboutId))
            {
                throw new ArgumentException("An id cannot be null, empty or whitespace.", nameof(aboutId));
            }

            if (string.Equals(npcId, aboutId, StringComparison.Ordinal))
            {
                throw new ArgumentException("An NPC cannot hold an opinion about itself.", nameof(aboutId));
            }
        }

        #endregion
    }
}
