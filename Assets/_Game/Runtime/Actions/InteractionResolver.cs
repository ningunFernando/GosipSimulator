using System;
using System.Collections.Generic;
using UnityEngine;

namespace GosipSimulator.Actions
{
    /// <summary>
    /// Which thing the player is acting on. Plain C# and given positions as data, so reach is a rule
    /// that runs in an EditMode test rather than something only reproducible by standing in the right
    /// spot (R5).
    ///
    /// This is where the plan said ActionCatalog would go. A catalog turned out to have no work to do:
    /// every Interactable already carries its own ActionDefinitionSO, so there is nothing to look an id
    /// up in, and a type that only forwards is the silent stub R12 forbids. What the module actually
    /// needed was this: a reach rule with a defined answer when two things are equally close.
    /// </summary>
    public class InteractionResolver
    {
        /// <summary>Nothing was within reach.</summary>
        public const int NONE = -1;

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// The index of the nearest position within <paramref name="reach"/>, or NONE.
        /// </summary>
        /// <param name="reach">
        /// World units. Zero or less reaches nothing, which is a valid way to switch interaction off
        /// rather than an error.
        /// </param>
        public int Resolve(Vector3 from, float reach, IReadOnlyList<Vector3> positions)
        {
            if (positions == null) throw new ArgumentNullException(nameof(positions));

            // Negated so a NaN reach is rejected too, instead of failing every comparison below in
            // silence and looking like a player who simply cannot reach anything.
            if (!(reach > 0f)) return NONE;

            int   best         = NONE;
            float bestDistance = float.MaxValue;
            float reachSq      = reach * reach;

            for (int i = 0; i < positions.Count; i++)
            {
                float distanceSq = (positions[i] - from).sqrMagnitude;

                if (distanceSq > reachSq) continue;

                // Strictly nearer, so an exact tie keeps the earlier index. Two objects the same
                // distance away is rare in play and certain in a test, and "whichever the loop saw
                // first" is only a rule if the loop order is fixed, which it is here.
                if (distanceSq >= bestDistance) continue;

                best         = i;
                bestDistance = distanceSq;
            }

            return best;
        }

        #endregion
    }
}
