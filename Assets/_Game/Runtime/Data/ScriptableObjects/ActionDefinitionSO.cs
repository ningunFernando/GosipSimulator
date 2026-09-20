using UnityEngine;

namespace GosipSimulator.Data
{
    [CreateAssetMenu(fileName = "NewActionDefinition", menuName = "GosipSimulator/Action Definition")]
    public class ActionDefinitionSO : ScriptableObject
    {
    
        // ────────────────────────────────
        // IDENTITY
        // ────────────────────────────────
        #region Identity

        [Header("Identity")]
        [Tooltip("The unique identifier for this action. This should be a string that uniquely identifies the action in the game.")]
        [SerializeField] private string _id;

        [Tooltip("The display name of the action. This is the name that will be shown to players. Never used as an identifier, only for display purposes.")]
        [SerializeField] private string _displayName;
        #endregion


        // ────────────────────────────────
        // SOCIAL WEIGHT
        // ────────────────────────────────
        #region Social Weight

        [Header("Social Weight")]
        [Tooltip("The base delta value for the social weight of this action.")]
        [SerializeField] private int _baseDelta = -10;
        #endregion


        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public string Id => _id;
        public string DisplayName => _displayName;
        public int BaseDelta => _baseDelta;

        #endregion
    }
}
