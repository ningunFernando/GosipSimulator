using UnityEditor;
using System.Collections.Generic;
using GosipSimulator.Data;

namespace GosipSimulator.Tools
{
    /// <summary>
    /// The one place in the tool that goes to disk. It finds the NPC definitions and copies them into
    /// snapshots, which is what keeps everything above it working on plain data and testable without
    /// a project 
    /// </summary>
    public static class VillageAssets
    {
        private const string NPC_FILTER = "t:NpcDefinitionSO";

        // ────────────────────────────────
        //PUBLIC API
        // ────────────────────────────────
        #region Public Api

        public static List<NpcDefinitionSO> LoadAll()
        {
            string[] guids = AssetDatabase.FindAssets(NPC_FILTER);

            var definitions = new List<NpcDefinitionSO>(guids.Length);

            for(int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var definition = AssetDatabase.LoadAssetAtPath<NpcDefinitionSO>(path);

                if(definition != null) definitions.Add(definition);
            }

            definitions.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a),
            AssetDatabase.GetAssetPath(b)));

            return definitions;
        }


        public static List<NpcSnapshot> ToSnapshots(IReadOnlyList<NpcDefinitionSO> definitions)
        {
            var snapshots = new List<NpcSnapshot>(definitions.Count);
            for(int i = 0; i < definitions.Count; i++)
            {
                snapshots.Add(ToSnapshot(definitions[i]));
            }
                return snapshots;
        }

        public static NpcSnapshot ToSnapshot(NpcDefinitionSO definition)
        {
            IReadOnlyList<SocialTie> ties = definition.Ties;

            var copied = new List<TieSnapshot>(ties?.Count ?? 0);

            if(ties != null)
            {
                for(int i = 0; i < ties.Count; i++)
                {
                    if (ties[i] == null) continue;

                    copied.Add(new TieSnapshot(ties[i].OtherNpcId, ties[i].Trust));
                }
            }

            return new NpcSnapshot(definition.Id, definition.DisplayName, copied);
        }
        #endregion
    }
}
