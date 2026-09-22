using UnityEditor;
using UnityEngine.UIElements;

namespace GosipSimulator.Tools
{
    public class VillageGraphWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "Village Graph";
        private const string MENU_PATH = "Window/Gossip Simulator/Village Graph";

        // ────────────────────────────────
        // Menu
        // ────────────────────────────────
        #region Menu
        [MenuItem(MENU_PATH)]
        private static void OpenWindow()
        {
            GetWindow<VillageGraphWindow>(WINDOW_TITLE);
        }
        #endregion   


        // ────────────────────────────────
        //LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void CreateGUI()
        {
            //CreateGUI rather than OnEnable is used because it is called when the window is opened and when the window is reloaded after a script compilation.
            var graph = new VillageGraphView();

            graph.StretchToParentSize();
            rootVisualElement.Add(graph);
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private
        #endregion
    }
}
