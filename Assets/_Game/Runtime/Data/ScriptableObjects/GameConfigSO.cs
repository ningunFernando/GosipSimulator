using UnityEngine;

namespace GosipSimulator.Data
{
    /// <summary>
    /// Global game configuration. Data owns the values and everyone else reads them.
    /// This assembly references nothing (R3), so it can only hold primitives, UnityEngine
    /// types and enums declared here: a Core type such as GameState or PoolConfig cannot
    /// appear in this file without inverting the graph.
    /// </summary>
    [CreateAssetMenu(fileName = "NewGameConfig", menuName = "GosipSimulator/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        // ────────────────────────────────
        // BOOTSTRAP
        // ────────────────────────────────
        #region Bootstrap

        [Header("Bootstrap")]
        [Tooltip("Scene loaded once the bootstrap sequence completes. Must be added to Build Settings.")]
        [SerializeField] private string _gameSceneName = "Scene_Game";

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// Read only on purpose. A public mutable field here would let any system rewrite the
        /// shared configuration at runtime and leave no trace of who did it (M6).
        /// </summary>
        public string GameSceneName => _gameSceneName;

        #endregion
    }
}
