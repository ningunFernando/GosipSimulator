using System;
using System.Collections.Generic;

namespace GosipSimulator.Gossip
{
    public class RumorPropagator
    {
        public struct Step
        {
            public string fromId;
            public string toId;
            public int hop;
            public int weight;
        }

        public struct FrontierEntry
        {
            public string id;
            public int hop;
            public int weight;
        }

        private readonly SocialGraph _social;
        private readonly int _decayPercentPerHop;
        private readonly int _maxHops;

        // ────────────────────────────────
        // CONSTRUCTOR
        // ────────────────────────────────
        #region Constructor
        /// <param name="decayPercentPerHop">Percentage of the story lost per telling. 0 carries it undiminished.</param>
        /// <param name="maxHops">How many introductions away anyone can be and still hear it. 0 means nobody does.</param>
        
        public RumorPropagator(SocialGraph social, int decayPercentPerHop, int maxHops)
        {
            if (social == null) throw new ArgumentNullException(nameof(social));

            if(decayPercentPerHop < 0 || decayPercentPerHop > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(decayPercentPerHop), decayPercentPerHop,
                    "Decay percent per hop must be 0 to 100.");
            }
        
            if(maxHops < 0)
            {
                throw new ArgumentException($"Max hops must be 0 or greater, but was {maxHops}.", nameof(maxHops));
            }

            _social = social;
            _decayPercentPerHop = decayPercentPerHop;
            _maxHops = maxHops;
        }
        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API
        
        public IReadOnlyList<Step> Plan(string sourceId, int initialWeight)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("A source id cannot be null, empty or whitespace.", nameof(sourceId));
            }

            // Nothing to carry. Checked before touching the graph so a neutral action plans nothing
            // instead of walking the whole village to discover every hop rounds to zero.
            if (initialWeight == 0) return new Step[0];

            var informed = new HashSet<string>(StringComparer.Ordinal) { sourceId };
            var steps    = new List<Step>();

            var frontier = new List<FrontierEntry>
            {
                new FrontierEntry { id = sourceId, weight = initialWeight, hop = 0 }
            };

            while (frontier.Count > 0)
            {
                var next = new List<FrontierEntry>();

                for (int i = 0; i < frontier.Count; i++)
                {
                    FrontierEntry entry = frontier[i];

                    if (entry.hop >= _maxHops) continue;

                    IReadOnlyList<SocialGraph.Tie> ties = _social.TiesOf(entry.id);

                    for (int t = 0; t < ties.Count; t++)
                    {
                        string otherId = ties[t].otherId;

                        if (informed.Contains(otherId)) continue;

                        int carried = Carry(entry.weight, ties[t].trust);

                        // Too faint to move anybody. Not queued and not published, so a listener
                        // never sees a rumor that changed nothing.
                        if (carried == 0) continue;

                        informed.Add(otherId);

                        steps.Add(new Step
                        {
                            fromId = entry.id,
                            toId   = otherId,
                            hop    = entry.hop + 1,
                            weight = carried
                        });

                        next.Add(new FrontierEntry { id = otherId, weight = carried, hop = entry.hop + 1 });
                    }
                }

                frontier = next;
            }

            return steps;
        }


        #endregion
        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        /// <summary>
        /// Calculates how much of the story is carried to the next NPC, given the weight of the story and the trust of the tie.
        /// The story is also decayed by the decay percent per hop.
        /// </summary>
        /// <param name="weight">The weight of the story.</param>
        /// <param name="trust">The trust of the tie.</param>
        /// <returns>The amount of the story carried to the next NPC.</returns>
        private int Carry(int weight, int trust)
        {
            long carried = (long)weight * trust/100;
            carried = carried * (100 - _decayPercentPerHop) / 100;

            if (carried > int.MaxValue) return int.MaxValue;
            if (carried < int.MinValue) return int.MinValue;

            return (int)carried;
        }

        #endregion
    }
}
