using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GosipSimulator.Core;

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
        #endregion
    }
}
