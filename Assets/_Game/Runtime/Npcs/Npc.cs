using UnityEngine;
using GosipSimulator.Core;
using GosipSimulator.Data;

namespace GosipSimulator.Npcs
{
    /// <summary>
    /// One villager in the scene: an identity and a pair of eyes. Deliberately almost empty, because
    /// everything an NPC does for now is decided elsewhere: perception is a rule in
    /// PerceptionResolver, and what an NPC thinks is owned by Gossip and reaches it through the bus.
    ///
    /// The id comes from the NpcDefinitionSO rather than a string field, so the same asset names this
    /// NPC here and names them in the social graph the gossip config builds. Two places typing
    /// "blacksmith" by hand is one typo away from a villager nobody can gossip about.
    /// </summary>
    public class Npc : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Identity")]
        [Tooltip("The asset that names this NPC. The same one the gossip config uses for the social graph.")]
        [SerializeField] private NpcDefinitionSO _definition;

        [Header("Perception")]
        [Tooltip("How far this NPC can see, in world units. Zero means they notice nothing.")]
        [SerializeField] private float _sightRange = 6f;

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>The id from the definition, or null when nothing is assigned.</summary>
        public string Id => _definition != null ? _definition.Id : null;

        /// <summary>For the log and a future HUD. Never used to identify anybody.</summary>
        public string DisplayName => _definition != null ? _definition.DisplayName : null;

        public float SightRange => _sightRange;

        #endregion

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void OnValidate()
        {
            // The Inspector stores a negative range without complaining, and a negative range reads
            // as "sees nothing" instead of as the mistake it is (R8).
            _sightRange = Mathf.Max(0f, _sightRange);
        }

        private void Awake()
        {
            // Checked here too because OnValidate never runs in a build (R8). An NPC with no identity
            // cannot be gossiped about, and staying enabled would put a nameless candidate in front
            // of perception on every action (R9).
            if (_definition == null)
            {
                Log.Error($"[Npc] '{name}' has no NPC definition assigned. It will not witness anything.");
                enabled = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(_definition.Id))
            {
                Log.Error($"[Npc] '{name}' uses '{_definition.name}', whose id is empty. It will not witness anything.");
                enabled = false;
            }
        }

        #endregion
    }
}
