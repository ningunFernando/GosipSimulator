using System;
using GosipSimulator.Core;
using GosipSimulator.Data;
using UnityEditor;

namespace GosipSimulator.Tools
{
    public static class VillageEdits
    {
        public const string TIES = "_ties";
        public const string OTHER_ID = "_otherNpcId";
        public const string TRUST = "_trust";

        public const int MIN_TRUST = 0;
        public const int MAX_TRUST = 100;

        public const int DEFAULT_TRUST = 50;

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>Adds a tie at the end of the list. False when nothing was written.</summary>
        public static bool AddTie(NpcDefinitionSO from, string toId, int trust)
        {
            if (from == null || string.IsNullOrWhiteSpace(toId)) return false;

            if(trust < MIN_TRUST || trust > MAX_TRUST) return false;

            var serialized = new SerializedObject(from);
            SerializedProperty ties = serialized.FindProperty(TIES);

            if( ties == null) return Broken(from, TIES);

            int index = ties.arraySize;

            // Growing the array copies the last tie into the new slot, or leaves defaults when the list
            // was empty. Both fields are written below either way, so whatever it copied never shows.

            ties.arraySize = index + 1;

            SerializedProperty tie = ties.GetArrayElementAtIndex(index);
            SerializedProperty other = tie.FindPropertyRelative(OTHER_ID); 
            SerializedProperty value = tie.FindPropertyRelative(TRUST);

            if (other == null) return Broken(from, OTHER_ID);
            if (value == null) return Broken(from, TRUST);

            other.stringValue = toId;
            value.intValue = trust;

            serialized.ApplyModifiedProperties();
            Undo.SetCurrentGroupName($"Tie {from.Id} to {toId}");

            return true;
        }

        /// <summary>
        /// Removes the first tie to that NPC ( false when theres none)
        /// </summary>
        public static bool RemoveTie(NpcDefinitionSO from, string toId)
        {

            if (from == null || string.IsNullOrWhiteSpace(toId)) return false;

            var serialized = new SerializedObject(from);
            SerializedProperty ties = serialized.FindProperty(TIES);

            if (ties == null) return Broken(from, TIES);

            for(int i = 0; i < ties.arraySize; i++)
            {
                SerializedProperty other = ties.GetArrayElementAtIndex(i).FindPropertyRelative(OTHER_ID);

                if (other == null) return Broken(from, OTHER_ID);

                if (!string.Equals(other.stringValue, toId, StringComparison.Ordinal)) continue;

                ties.DeleteArrayElementAtIndex(i);

                serialized.ApplyModifiedProperties();
                Undo.SetCurrentGroupName($"Untie {from.Id} from {toId}");

                return true;
            }

            return false;
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private
        private static bool Broken(NpcDefinitionSO asset, string field)
        {
            Log.Error($"[VillageGraph] '{asset.name}' has no serialized field '{field}'. NpcDefinitionSO " + 
            "changed whitout VillageEdits following it, so nothing was written");

            return false;
        }
        #endregion
    }
}
