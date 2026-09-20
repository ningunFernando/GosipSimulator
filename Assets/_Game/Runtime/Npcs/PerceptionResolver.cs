using System;
using System.Collections.Generic;
using UnityEngine;

namespace GosipSimulator.Npcs
{
    /// <summary>
    /// Who saw it. Plain C# and given its candidates as data, so the rule is testable without a scene
    /// and without a single NPC in it (R5). It knows nothing about opinions or rumors: perception ends
    /// the moment the list of witnesses exists.
    ///
    /// Distance only, deliberately. A wall between two NPCs does not hide anything yet, and adding
    /// line of sight would mean physics, which is the adapter's world and not this one. When it is
    /// worth having, it goes in NpcRegistry as a filter over this answer, not in here.
    /// </summary>
    public class PerceptionResolver
    {
        /// <summary>One NPC as perception sees them: an id, a place and how far they can see.</summary>
        public struct Candidate
        {
            public string  npcId;
            public Vector3 position;

            /// <summary>World units. Zero means this NPC notices nothing, which is valid, not broken.</summary>
            public float sightRange;
        }

        // Reused across calls so a busy scene does not allocate a list per action, the same reason
        // GossipService keeps its own buffers.
        private readonly List<(string id, float distanceSq)> _hits = new List<(string, float)>();

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// Everyone who could see <paramref name="point"/> from where they stand, nearest first.
        /// </summary>
        /// <param name="actorId">
        /// Left out of the answer. Somebody standing where the action happened is always inside their
        /// own sight range, and an NPC witnessing themselves is a pair the opinion graph rejects.
        /// </param>
        public IReadOnlyList<string> Resolve(Vector3 point, string actorId, IReadOnlyList<Candidate> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            _hits.Clear();

            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate = candidates[i];

                if (string.IsNullOrWhiteSpace(candidate.npcId)) continue;
                if (string.Equals(candidate.npcId, actorId, StringComparison.Ordinal)) continue;

                // Negated so a NaN range is rejected too. A NaN would fail the distance test below
                // anyway, but silently, and an NPC who never notices anything is hard to debug.
                if (!(candidate.sightRange > 0f)) continue;

                float distanceSq = (candidate.position - point).sqrMagnitude;

                // Squared on both sides: the comparison is the same and there is no square root.
                if (distanceSq > candidate.sightRange * candidate.sightRange) continue;

                _hits.Add((candidate.npcId, distanceSq));
            }

            // Nearest first, and ties broken by id. The order decides which witness forms an opinion
            // first and therefore which rumor is queued first, so leaving it to however the scene
            // happened to be authored would make the same theft play out differently between runs.
            // Same reason SocialGraph keeps its ties sorted.
            _hits.Sort(CompareHits);

            var witnesses = new string[_hits.Count];

            for (int i = 0; i < _hits.Count; i++) witnesses[i] = _hits[i].id;

            return witnesses;
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private static int CompareHits((string id, float distanceSq) a, (string id, float distanceSq) b)
        {
            int byDistance = a.distanceSq.CompareTo(b.distanceSq);

            return byDistance != 0 ? byDistance : string.CompareOrdinal(a.id, b.id);
        }

        #endregion
    }
}
