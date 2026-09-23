using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GosipSimulator.Core;
using System.Collections.Generic;
using GosipSimulator.Data;
using System;
using UnityEngine;
using System.Text;

namespace GosipSimulator.Tools
{
    public class VillageGraphView : GraphView
    {
        // A path to the style sheet for the graph view. the reason for a path intead of a serialized reference is so the windos need no setup in the inspector
        private const string STYLE_SHEET_PATH = "Assets/_Game/Tools/VillageGraph/VillageGraph.uss";

        public VillageGraphView()
        {
           
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            AddGrid();
            AddStyleSheet();
        }

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public Api
        /// <summary>
        /// Draws the village as the assets have it right now. Everything already on the canvas is
        /// thrown away first rather than patched: the assets are the truth, and a full redraw is the
        /// cheapest way to be sure the canvas is not showing a village that has already changed (R7).
        /// </summary>
        public void Populate()
        {
            DeleteElements(graphElements.ToList());

            List<NpcDefinitionSO> definitions = VillageAssets.LoadAll();
            VillageGraphData data = VillageRules.Build(VillageAssets.ToSnapshots(definitions));

            Dictionary<string, NpcNode> nodesById = AddNodes(data, definitions);

            for (int i = 0; i < data.Edges.Count; i ++) Connect(data.Edges[i], nodesById);

            LogProblems(data.Problems);
        }
        

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private void AddGrid()
        {
            var grid = new GridBackground();

            Insert(0, grid);
            grid.StretchToParentSize();
        }

        private void AddStyleSheet()
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_SHEET_PATH);

            if(sheet == null)
            {
                Log.Error($"[VillageGraphView] StyleSheet not found at path: {STYLE_SHEET_PATH}");
                return;
            }
            styleSheets.Add(sheet);
        }

        private Dictionary<string, NpcNode> AddNodes(VillageGraphData data, IReadOnlyList<NpcDefinitionSO> definitions)
        {
            var nodesById = new Dictionary<string, NpcNode>(data.Npcs.Count, StringComparer.Ordinal);

            for (int i = 0; i < data.Npcs.Count; i++)
            {
                var node = new NpcNode(data.Npcs[i], definitions[i]);

                node.SetPosition(new Rect(VillageRules.FallbackPosition(i), Vector2.zero));
                AddElement(node);

                if(!string.IsNullOrWhiteSpace(node.Id) && !nodesById.ContainsKey(node.Id)) nodesById.Add(node.Id,node);
            }       

            return nodesById;     
        }

        private void Connect(VillageEdge tie, IReadOnlyDictionary<string, NpcNode> nodesById)
        {
            if(!nodesById.TryGetValue(tie.FromId, out NpcNode from) || !nodesById.TryGetValue(tie.ToId, out NpcNode to))
            {
                Log.Error($"[VillageGraph] No Node for the tie '{tie.FromId}' to '{tie.ToId}'. The canvas is out of step with the rules");

                return;
            }

            Edge edge = from.Tells.ConnectTo(to.Hears);
            
            edge.tooltip = $"{tie.FromId} tells {tie.ToId}, trust: {tie.Trust}";

            edge.capabilities &= ~Capabilities.Deletable;

            AddElement(edge);
        }

        private static void LogProblems(IReadOnlyList<VillageProblem> problems)
        {
            if(problems.Count == 0) return;

            var text = new StringBuilder("[VillageGraph] The assets say thing that the graph cannot draw");

            for(int i = 0; i < problems.Count; i++) text.Append("\n").Append(problems[i].Message);

            Log.Warn(text.ToString());
        }
        #endregion
    }
}
