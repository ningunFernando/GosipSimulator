using UnityEngine;
using GosipSimulator.Core;
using GosipSimulator.Data;

namespace GosipSimulator.Actions
{
    /// <summary>
    /// Something in the world the player can act on, and the declaration of what acting on it means.
    /// The verb lives in an ActionDefinitionSO rather than in a string field here, so the same asset
    /// that says a robbery is worth -10 is the one that names it on this object.
    ///
    /// It holds no behaviour at all. InteractionReader decides when the player acted, Npcs decides who
    /// saw it and Gossip decides what it costs; this object only says what it is (R11).
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Action")]
        [Tooltip("What acting on this means. The same asset the gossip config scores.")]
        [SerializeField] private ActionDefinitionSO _action;

        [Tooltip("Who it is done to, as an NPC id. Left empty when the action has no victim.")]
        [SerializeField] private string _targetId;

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>The action id from the definition, or null when nothing is assigned.</summary>
        public string ActionId => _action != null ? _action.Id : null;

        /// <summary>For the log and a future prompt. Never used to identify anything.</summary>
        public string DisplayName => _action != null ? _action.DisplayName : null;

        /// <summary>Never null: an action with no victim carries an empty target, not a missing one.</summary>
        public string TargetId => _targetId ?? string.Empty;

        #endregion

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            // OnValidate never runs in a build, so the same check happens here (R8). Disabled rather
            // than left in the world, because an interactable with no verb would sit in reach
            // swallowing the player's attempts without ever publishing anything (R9).
            if (_action == null)
            {
                Log.Error($"[Interactable] '{name}' has no action assigned. It cannot be acted on.");
                enabled = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(_action.Id))
            {
                Log.Error($"[Interactable] '{name}' uses '{_action.name}', whose id is empty. It cannot be acted on.");
                enabled = false;
            }
        }

        #endregion
    }
}
