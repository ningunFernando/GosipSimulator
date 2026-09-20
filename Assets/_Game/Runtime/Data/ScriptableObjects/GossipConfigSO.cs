using UnityEngine;

namespace GosipSimulator.Data
{
    [CreateAssetMenu(fileName = "NewGossipConfig", menuName = "GosipSimulator/Gossip Config", order = 1)]
    public class GossipConfigSO : ScriptableObject
    {
        // ────────────────────────────────
        // OPINION RANGE
        // ────────────────────────────────
        #region Opinion Range

        [Header("Opinion Range")]
        [Tooltip("The minimum opinion value an NPC can have towards another NPC.")]
        [SerializeField] private int _opinionMin = -100;

        [Tooltip("The maximum opinion value an NPC can have towards another NPC.")]
        [SerializeField] private int _opinionMax = 100;

        #endregion

        // ────────────────────────────────
        // PROPAGATION
        // ────────────────────────────────
        #region Propagation

        [Header("Propagation")]
        [Tooltip("The rate at which gossip spreads through the network.")]
        [SerializeField] private int _decayPercentPerHop = 40;

        [Tooltip("The maximum number of hops gossip can travel.")]
        [SerializeField] private int _maxHops = 3;

        [Tooltip("The delay in seconds between each hop of gossip propagation.")]
        [SerializeField] private float _hopDelay = 5f; 

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public int OpinionMin => _opinionMin;

        public int OpinionMax => _opinionMax;

        public int DecayPercentPerHop => _decayPercentPerHop;

        public int MaxHops => _maxHops;

        public float HopDelay => _hopDelay;

        #endregion
    }
}
