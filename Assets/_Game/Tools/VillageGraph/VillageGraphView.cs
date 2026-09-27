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
        private VillageLayoutSO _layout;

        public VillageGraphView()
        {
           
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            AddGrid();
            AddStyleSheet();
            graphViewChanged = OnGraphViewChanged;
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
            foreach (GraphElement element in graphElements.ToList()) RemoveElement(element);

            _layout = VillageAssets.LoadLayout();

            List<NpcDefinitionSO> definitions = VillageAssets.LoadAll();
            VillageGraphData data = VillageRules.Build(VillageAssets.ToSnapshots(definitions));

            Dictionary<string, NpcNode> nodesById = AddNodes(data, definitions);

            for (int i = 0; i < data.Edges.Count; i ++) Connect(data.Edges[i], nodesById);

            LogProblems(data.Problems);
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatible = new List<Port>();

            foreach (Port port in ports.ToList())
            {
                //An Edge joins an output to an input on two dferent nodes, carrying the same thing.
                if (port.direction == startPort.direction) continue;
                if(port.node == startPort.node) continue;
                if(port.portType != startPort.portType) continue;

                Port output = startPort.direction == Direction.Output ? startPort : port;
                Port input = startPort.direction == Direction.Output ? port : startPort;

                if (output.node is NpcNode from && input.node is NpcNode to && CanTie(from, to))
                {
                    compatible.Add(port);
                }
            }

            return compatible;
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
                string guid = VillageAssets.GuidOf(definitions[i]);
                var node = new NpcNode(data.Npcs[i], definitions[i], guid);

                if(_layout == null || !_layout.TryGet(guid, out Vector2 position))
                {
                    position = VillageRules.FallbackPosition(i);
                }

                node.SetPosition(new Rect(position, Vector2.zero));
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
            
            edge.tooltip = TooltipFor(tie.FromId, tie.ToId, tie.Trust);

            AddElement(edge);
        }

        private static void LogProblems(IReadOnlyList<VillageProblem> problems)
        {
            if(problems.Count == 0) return;

            var text = new StringBuilder("[VillageGraph] The assets say thing that the graph cannot draw");

            for(int i = 0; i < problems.Count; i++) text.Append("\n").Append(problems[i].Message);

            Log.Warn(text.ToString());
        }

        /// <summary>
        /// Everything the user does to the canvas arrives here before GraphView applies it. Returning
        /// the change lets it through, which is all this milestone needs; milestone 4 starts refusing
        /// some of them.
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.movedElements != null) RememberPositions(change.movedElements);
            if (change.edgesToCreate    != null) WriteNewTies(change.edgesToCreate);
            if (change.elementsToRemove != null) RemoveDeletedTies(change.elementsToRemove);

            return change;
        }

        private void RememberPositions(List<GraphElement> moved)
        {
            VillageLayoutSO layout = VillageAssets.LoadOrCreateLayout();

            bool changed = false;

            for (int i = 0; i < moved.Count; i++)
            {
                // Edges move with their nodes and have no position of their own to remember.
                if (!(moved[i] is NpcNode node)) continue;

                changed |= layout.Set(node.Guid, node.GetPosition().position);
            }

            // A drag that ended where it started is not worth an import of the asset.
            if (!changed) return;

            _layout = layout;

            VillageAssets.SaveLayout(layout);
        }

        private static bool CanTie(NpcNode from, NpcNode to)
        {
            // Read from the asset rather than from the node, so a tie written since the last redraw
            // counts.
            return VillageRules.CanTie(from.Id, to.Id, VillageAssets.ToSnapshot(from.Definition).Ties);
        }

        private static void WriteNewTies(List<Edge> edges)
        {
            for (int i = edges.Count - 1; i >= 0; i--)
            {
                Edge edge = edges[i];

                if (edge.output.node is NpcNode from && edge.input.node is NpcNode to && CanTie(from, to) &&
                VillageEdits.AddTie(from.Definition, to.Id, VillageEdits.DEFAULT_TRUST))
                {
                    edge.tooltip = TooltipFor(from.Id, to.Id, VillageEdits.DEFAULT_TRUST);
                    continue;
                }
                //Refused, so graphview never draws it
                edges.RemoveAt(i);
            }
        }

                /// <summary>The same deal in reverse: a line only goes away when its tie did.</summary>
        private static void RemoveDeletedTies(List<GraphElement> elements)
        {
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                // Nodes cannot be deleted until milestone 8, so edges are all this can be today.
                if (!(elements[i] is Edge edge)) continue;

                if (edge.output.node is NpcNode from && edge.input.node is NpcNode to &&
                    VillageEdits.RemoveTie(from.Definition, to.Id))
                {
                    continue;
                }

                elements.RemoveAt(i);
            }
        }

        private static string TooltipFor(string fromId, string toId, int trust)
        {
            return $"{fromId} tells {toId}. Trust:{trust}";
        }
        #endregion
    }
}
