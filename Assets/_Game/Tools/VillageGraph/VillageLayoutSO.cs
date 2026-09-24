using UnityEngine;
using System;
using System.Collections.Generic;

namespace GosipSimulator.Tools
{
    /// <summary>
    /// Location on where each NPC node sits on the canvas. Editor data only thats why theres not a field for NPC definitionSO.
    /// Position Just for the tool
    /// GUID keys survive renames, and an asset gets committed where EditorPrefs wouldn't.
    /// </summary>
    public class VillageLayoutSO : ScriptableObject
    {
        [Serializable]
        private class Placement
        {
            public string guid;
            public Vector2 position;
        }

        [SerializeField] private List<Placement> _placements = new List<Placement>();

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// How many nodes have been placed by hand
        /// </summary>
        public int Count => _placements.Count;

        public bool TryGet(string guid, out Vector2 position)
        {
            position = Vector2.zero;

            if(string.IsNullOrEmpty(guid)) return false;

            Placement placement = Find(guid);

            if(placement == null) return false;

            position = placement.position;

            return true;
        }


        /// <summary>
        /// Remembers where a node was left. True when something actualy changed, 
        /// so the caller skips writing the asset to disk for a false drag
        /// </summary>
        public bool Set(string guid, Vector2 position)
        {
            if (string.IsNullOrEmpty(guid)) return false;

            Placement placement = Find(guid);

            if (placement == null)
            {
                _placements.Add(new Placement { guid = guid, position = position});
                return true;
            }

            if(placement.position == position) return false;

            placement.position = position;

            return true;
        }

        /// <summary>Drops an NPC's spot. True when there was one to drop.</summary>
        public bool Forget(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return false;

            Placement placement = Find(guid);

            if (placement == null) return false;

            return _placements.Remove(placement);
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private Placement Find(string guid)
        {
            for (int i = 0; i < _placements.Count; i++)
            {
                if(string.Equals(_placements[i].guid, guid, StringComparison.Ordinal)) return _placements[i];
            }

            return null;
        }
        #endregion
    }
}
