using System;
using System.Collections.Generic;
using UnityEngine;

namespace GosipSimulator.Tools
{
    public static class VillageRules
    {
        /// <summary>
        /// How many rows a fallback row holds before the next row is created.
        /// </summary>
        public const int COLUMNS = 3;

        /// <summary>
        /// The spacing between village elements. wide enough for a node and its ports.
        /// </summary>
        public static readonly Vector2 SPACING = new Vector2(280f, 180f);

        // ────────────────────────────────
        //PUBLIC API
        // ────────────────────────────────
        #region Public API

        public static VillageGraphData Build(IReadOnlyList<NpcSnapshot> npcs)
        {
            var edges = new List<VillageEdge>();
            var problems = new List<VillageProblem>();

            if(npcs == null) return new VillageGraphData(Array.Empty<NpcSnapshot>(), edges, problems);

            HashSet<string> declared = DeclaredIds(npcs, problems);

            var drawn = new HashSet<(string from, string to)>();

            for(int i = 0; i < npcs.Count; i++)
            {
                string fromId = npcs[i].Id;

                if(string.IsNullOrWhiteSpace(fromId)) continue;

                IReadOnlyList<TieSnapshot> ties = npcs[i].Ties;

                for(int t = 0; t < ties.Count; t++)
                {
                    AddTie(fromId, ties[t], declared, drawn, edges, problems);
                }
            }

            return new VillageGraphData(npcs, edges, problems);
        }

        /// <summary>
        /// Where a node goes when the layout has never seen it: in reading order, left to right.
        /// </summary>
        public static Vector2 FallbackPosition(int index)
        {
            return new Vector2((index % COLUMNS) * SPACING.x, (index/COLUMNS) * SPACING.y);
        }
        
        #endregion

        // ────────────────────────────────
        //PRIVATE
        // ────────────────────────────────
        #region Private

        private static HashSet<string> DeclaredIds(IReadOnlyList<NpcSnapshot> npcs, List<VillageProblem> problems)
        {
            var declared = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < npcs.Count; i++)
            {
                string id = npcs[i].Id;

                if (string.IsNullOrWhiteSpace(id))
                {
                    problems.Add(new VillageProblem(string.Empty, 
                    $"'{npcs[i].DisplayName}' has no Id, so nothing can be tied to it"));

                    continue;
                }

                if (!declared.Add(id))
                {
                    problems.Add(new VillageProblem(id, $"More than one definition declares the Id '{id}'."));
                }
            }

            return declared;
        }


        private static void AddTie(string fromId, TieSnapshot tie, HashSet<string> declared, 
        HashSet<(string from, string to)> drawn, List<VillageEdge> edges, List<VillageProblem> problems)
        {
            string toId = tie.OtherNpcId;

            if (string.IsNullOrWhiteSpace(toId))
            {
                problems.Add(new VillageProblem(fromId, $"'{fromId}' has a tie with nobody on the other end"));

                return;
            }

            if(StringComparer.Ordinal.Equals(fromId, toId))
            {
                problems.Add(new VillageProblem(fromId, $"'{fromId}' has a tie with themselves"));

                return;
            }

            if (!declared.Contains(toId))
            {
                problems.Add(new VillageProblem(fromId, $"'{fromId}' has a tie to '{toId}', which no definition declares"));
                return;
            }

            if(!drawn.Add((fromId, toId)))
            {
                problems.Add(new VillageProblem(fromId, $"'{fromId}' has more than one tie to '{toId}', only the first is drawn"));

                return;
            }
            edges.Add(new VillageEdge(fromId, toId, tie.Trust));
        }
        #endregion
    }
}
