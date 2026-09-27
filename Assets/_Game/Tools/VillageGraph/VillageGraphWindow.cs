using UnityEditor;
using UnityEngine.UIElements;

namespace GosipSimulator.Tools
{
    public class VillageGraphWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "Village Graph";
        private const string MENU_PATH = "Window/Gossip Simulator/Village Graph";
        private VillageGraphView _graph;
        private bool _isSubscribed;

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

        private void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;

            _isSubscribed = true;           
        }

        private void OnDisable()
        {
            if (!_isSubscribed) return;

            Undo.undoRedoPerformed -= HandleUndoRedo;

            _isSubscribed = false;        
        }

        private void CreateGUI()
        {
            //CreateGUI rather than OnEnable is used because it is called when the window is opened and when the window is reloaded after a script compilation.
            _graph = new VillageGraphView();

            _graph.StretchToParentSize();
            rootVisualElement.Add(_graph);

            _graph.Populate();
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private void HandleUndoRedo()
        {
            _graph?.Populate();
        }

        #endregion
    }
}
