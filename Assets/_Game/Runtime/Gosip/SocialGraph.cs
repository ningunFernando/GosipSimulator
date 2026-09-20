using System;
using System.Collections.Generic;

/// <summary>
/// Who tells what to whom, and how much the listener trusts the teller. Plain C# and immutable
/// once built, so every rumor in flight can share one instance and the propagation rules stay
/// testable without an asset (R5). GossipManager builds this from the NpcDefinitionSO list;
/// nothing in here knows that type exists.
///
/// Ties are directed on purpose: the son confiding in his father does not imply the father
/// confides in the son. A two-way relationship is declared twice, which is more authoring and
/// buys asymmetric trust, and asymmetric trust is most of what makes gossip interesting.
/// </summary>

namespace GosipSimulator.Gossip
{
    public class SocialGraph
    {
        public struct Tie
        {
            public string otherId;

            ///<summary> Number from one to zero on how much the story is trusted by this NPC </summary>
            public int trust;
        }

        private readonly Dictionary<string, Tie[]> _ties = new Dictionary<string, Tie[]>();

        // ────────────────────────────────
        // CONSTRUCTOR
        // ────────────────────────────────
        #region Constructor
        
        public SocialGraph(IEnumerable<(string fromId, string toId, int trust)> ties){
            if (ties == null) throw new ArgumentNullException(nameof(ties));

            var grouped = new Dictionary<string, List<Tie>>();

            var seen = new HashSet<(string fromId, string toId)>();

            foreach ((string fromId, string toId, int trust) tie in ties)
            {
                ValidateId(tie.fromId, "fromId");
                ValidateId(tie.toId, "toId");

                if (string.Equals(tie.fromId, tie.toId, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"NPC '{tie.fromId}' cannot have a social tie to itself.");
                }

                if (tie.trust < 0 || tie.trust > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(ties), tie.trust,
                        $"Trust between '{tie.fromId}' and '{tie.toId}' must be 0 to 100.");
                }

                //A duplicated tie is two entries in an asset that were meant to be the one throewing name 
                // it here.

                if(!seen.Add((tie.fromId, tie.toId)))
                {
                    throw new ArgumentException($"Duplicate social tie from '{tie.fromId}' to '{tie.toId}'");
                }
                if(!grouped.TryGetValue(tie.fromId, out List<Tie> list))
                {
                    list = new List<Tie>();
                    grouped[tie.fromId] = list;
                }
                list.Add(new Tie { otherId = tie.toId, trust = tie.trust });
            }

            foreach(KeyValuePair<string, List<Tie>> entry in grouped)
            {
                Tie[] sorted = entry.Value.ToArray();
                Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.otherId, b.otherId));
                _ties[entry.Key] = sorted;
            }


        }
        #endregion
        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region PUBLIC API
        
        private static readonly Tie[] NoTies = new Tie[0];

        ///<summary> The NPCs this one confides in, ordered by id. Empty, never null, for a loner. </summary>
        
        public IReadOnlyList<Tie> TiesOf(string npcId)
        {
            ValidateId(npcId, nameof(npcId));

            return _ties.TryGetValue(npcId, out Tie[] ties) ? ties : NoTies;
        }

        //Zero when ther is no tie, which is also what a tie of trust zero behaves like
        public int Trust(string fromId, string toId)
        {
            IReadOnlyList<Tie> ties = TiesOf(fromId);

            for(int i = 0; i < ties.Count; i++)
            {
                if(string.Equals(ties[i].otherId, toId, StringComparison.Ordinal))
                {
                    return ties[i].trust;
                }
            }

            return 0;
        }

        public bool Knows(string fromId, string toId)
        {
            IReadOnlyList<Tie> ties = TiesOf(fromId);

            for(int i = 0; i< ties.Count; i++)
            {
                if(string.Equals(ties[i].otherId, toId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private static void ValidateId(string id, string paramName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"NPC ID cannot be null or whitespace.", paramName);
            }
        }
        #endregion
    }
}
