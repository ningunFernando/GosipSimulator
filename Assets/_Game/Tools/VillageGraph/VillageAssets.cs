using UnityEditor;
using System.Collections.Generic;
using GosipSimulator.Data;
using GosipSimulator.Core;
using UnityEngine;

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

        private const string LAYOUT_PATH = "Assets/_Game/Tools/VillageGraph/VillageLayout.asset";

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

        public static string GuidOf(UnityEngine.Object asset)
        {
            if(asset == null) return string.Empty;

            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        }

        public  static VillageLayoutSO LoadLayout()
        {
            return AssetDatabase.LoadAssetAtPath<VillageLayoutSO>(LAYOUT_PATH);
        }

        public static VillageLayoutSO LoadOrCreateLayout()
        {
            VillageLayoutSO layout = LoadLayout();

            if(layout != null) return layout;

            layout = ScriptableObject.CreateInstance<VillageLayoutSO>();

            AssetDatabase.CreateAsset(layout, LAYOUT_PATH);
            AssetDatabase.SaveAssets();

            Log.Trace($"[VillageGrapgh] Created the layout at {LAYOUT_PATH}");
            return layout;
        }

        public static void SaveLayout( VillageLayoutSO layout)
        {
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssetIfDirty(layout);            
        }
        #endregion
    }
}
