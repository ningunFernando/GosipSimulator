using System.Collections.Generic;

namespace GosipSimulator.Tools
{
    /// <summary>
    /// The data needed to draw the village graph, and to run the village rules on it.
    /// </summary>
    public sealed class VillageGraphData
    {
        public readonly IReadOnlyList<NpcSnapshot> Npcs;
        public readonly IReadOnlyList<VillageEdge> Edges;
        public readonly IReadOnlyList<VillageProblem> Problems;

        public VillageGraphData(IReadOnlyList<NpcSnapshot> npcs, IReadOnlyList<VillageEdge> edges, IReadOnlyList<VillageProblem> problems)
        {
            Npcs = npcs ?? new List<NpcSnapshot>();
            Edges = edges ?? new List<VillageEdge>();
            Problems = problems ?? new List<VillageProblem>();
        }
    }

    /// <summary>
    /// One line to draw the story that travells from one NPC to the other, and how much of the story survives the telling.
    /// </summary>
    public readonly struct VillageEdge
    {
        public readonly string FromId;
        public readonly string ToId;
        public readonly int Trust;

        public VillageEdge(string fromId, string toId, int trust)
        {
            FromId = fromId;
            ToId = toId;
            Trust = trust;
        }
    }

    /// <summary>
    /// Something the assets say that the grpah cannot draw. Npc Id is who to blame and its empty when nobody can be named.
    /// </summary>
    public readonly struct VillageProblem
    {
        public readonly string NpcId;
        public readonly string Message;

        public VillageProblem(string npcId, string message)
        {
            NpcId = npcId;
            Message = message;
        }
    }
}