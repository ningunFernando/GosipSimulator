using System;
using System.Collections.Generic;

namespace GosipSimulator.Tools
{
    /// <summary>
    /// What the graph needs to know about the NPCs, copied riough out of the NPC asse
    /// the village rules work on these rather than an NpcDefinitionSO so they stay plain data
    /// </summary>
    public readonly struct NpcSnapshot
    {   
        /// <summary>
        /// The unique id of the NPC, used to identify them in the graph and in the tie snapshots
        /// </summary>
        public readonly string Id;
        /// <summary>
        /// The display name of the NPC, not used to identify an NPC, but used to display them in the graph
        /// </summary>
        public readonly string DisplayName;
        /// <summary>
        /// Who this Npc tell the story to, in the orther the asset lists them
        /// </summary>
        public readonly IReadOnlyList<TieSnapshot> Ties;

        public NpcSnapshot(string id, string displayName, IReadOnlyList<TieSnapshot> ties)
        {
            Id = id;
            DisplayName = displayName;
            Ties = ties ?? Array.Empty<TieSnapshot>();
        }
    }

    /// <summary>
    ///  who they tell, and how much of the story survives the telling.
    /// Trust runs 0 to 100, and RumorPropagator carries weight * trust / 100 on to the next NPC.
    /// </summary>
    public readonly struct TieSnapshot
    {
        public readonly string OtherNpcId;
        public readonly int Trust;

        public TieSnapshot(string otherNpcId, int trust)
        {
            OtherNpcId = otherNpcId;
            Trust = trust;
        }
    }
}
