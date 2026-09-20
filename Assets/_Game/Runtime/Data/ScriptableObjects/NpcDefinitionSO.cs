using UnityEngine;
using System.Collections.Generic;

namespace GosipSimulator.Data
{
    [CreateAssetMenu(fileName = "NewNpcDefinition", menuName = "GosipSimulator/Npc Definition", order = 1)]
    public class NpcDefinitionSO : ScriptableObject
    {
    
        // ────────────────────────────────
        // IDENTITY
        // ────────────────────────────────
        #region Identity

        [Header("Identity")]
        [Tooltip("The unique identifier for this NPC. This should be a string that uniquely identifies the NPC in the game.")]
        [SerializeField] private string _id;

        [Tooltip("The display name of the NPC. This is the name that will be shown to players. Never used as an identifier, only for display purposes.")]
        [SerializeField] private string _displayName;

        #endregion

        // ────────────────────────────────
        // SOCIAL
        // ────────────────────────────────
        #region Social

        [Header("Social")]
        [Tooltip("The list of social ties this NPC has with other NPCs.")]
        [SerializeField] private List<SocialTie> _ties = new List<SocialTie>();

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public string Id => _id;
        public string DisplayName => _displayName;
        public IReadOnlyList<SocialTie> Ties => _ties;

        #endregion
    }
}
