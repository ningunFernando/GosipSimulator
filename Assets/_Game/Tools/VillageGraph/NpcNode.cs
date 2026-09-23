using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GosipSimulator.Data;

namespace GosipSimulator.Tools
{
    public class NpcNode : Node
    {
        /// <summary>
        /// Representation of the villager in the canvas
        /// </summary>
        public const string TELLS = "Tells";
        public const string HEARS = "Hears";

        private const string NO_ID = "(no id)";
        private const string ID_CLASS = "npc-node__id";

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>The asset this node stands for. Never null, and the target of every later edit.</summary>
        public NpcDefinitionSO Definition { get; }

        public string Id { get; }

        public Port Tells { get; }
        public Port Hears { get; }

        #endregion

        public NpcNode(NpcSnapshot snapshot, NpcDefinitionSO definition)
        {
            Definition = definition;
            Id = snapshot.Id;

            title = string.IsNullOrWhiteSpace(snapshot.DisplayName) ? definition.name : snapshot.DisplayName;

            var id = new Label(string.IsNullOrWhiteSpace(snapshot.Id) ? NO_ID : snapshot.Id);
            id.AddToClassList(ID_CLASS);
            extensionContainer.Add(id);

            Hears = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(TieSnapshot));
            Hears.portName = HEARS;
            inputContainer.Add(Hears);

            Tells = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(TieSnapshot));
            Tells.portName = TELLS;
            outputContainer.Add(Tells);

            capabilities &= ~Capabilities.Deletable;
            RefreshPorts();
            RefreshExpandedState();
        }
    }
}
