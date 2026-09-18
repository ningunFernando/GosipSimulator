using System.Collections.Generic;
using UnityEngine;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;

namespace GosipSimulator.Pickups
{
    /// <summary>
    /// Keeps one pickup on each spawn point, taking them from the pool and giving them back. The pool
    /// is read from GameManager once the bootstrap is complete, which is also why entering Play
    /// straight from the game scene spawns nothing. A collection reaches the rest of the game only as
    /// OnPickupCollected (R4): this module never sees Save or the HUD.
    /// </summary>
    public class PickupSpawner : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Pool")]
        [Tooltip("Pool id of the pickup prefab, as configured in ObjectPoolManager.")]
        [SerializeField] private string      _poolId       = "Pickup";

        [Header("Spawning")]
        [Tooltip("Where pickups appear. Each point holds at most one pickup at a time.")]
        [SerializeField] private Transform[] _spawnPoints;
        [Tooltip("Seconds of game time before a collected pickup reappears on its point. Stops while the game is paused.")]
        [SerializeField] private float       _respawnDelay = 2f;

        #endregion

        private readonly Dictionary<Pickup, int> _pointByPickup = new Dictionary<Pickup, int>();
        private readonly List<int>               _ready         = new List<int>();

        private RespawnQueue      _respawns;
        private ObjectPoolManager _pool;
        private bool              _isSubscribed;

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void OnValidate()
        {
            _respawnDelay = Mathf.Max(0f, _respawnDelay);
        }

        private void Awake()
        {
            // Checked here too because OnValidate never runs in a build (R8). Setting enabled keeps
            // OnEnable from running, but Unity calls OnDisable on the spot, which is why OnDisable only
            // undoes what OnEnable actually did (R9).
            if (!IsConfigured(out string problem))
            {
                Log.Error($"[PickupSpawner] {problem} Spawning is disabled.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _respawns = new RespawnQueue();

            EventBus.Subscribe<OnBootstrapComplete>(HandleBootstrapComplete);

            _isSubscribed = true;
        }

        private void Update()
        {
            if (_pool == null) return;

            // Scaled time on purpose: PausedState sets timeScale to 0 and respawns wait with it.
            _respawns.Tick(Time.deltaTime, _ready);

            for (int i = 0; i < _ready.Count; i++)
            {
                SpawnAt(_ready[i]);
            }
        }

        private void OnDisable()
        {
            if (!_isSubscribed) return;

            EventBus.Unsubscribe<OnBootstrapComplete>(HandleBootstrapComplete);

            // Pooled pickups live under the pool's DontDestroyOnLoad container, so unloading this scene
            // without returning them would leave them active in whatever scene comes next.
            ReturnOwnedPickups();

            _pool         = null;
            _isSubscribed = false;
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>Called once by a pickup this spawner placed, when the player touches it.</summary>
        internal void Collect(Pickup pickup)
        {
            if (!_pointByPickup.TryGetValue(pickup, out int pointIndex))
            {
                Log.Warn($"[PickupSpawner] Collect called for '{pickup.name}', which this spawner does not own. Ignored.");
                return;
            }

            int value = pickup.Value;

            _pointByPickup.Remove(pickup);
            _pool.Return(_poolId, pickup.gameObject);
            _respawns.Schedule(pointIndex, _respawnDelay);

            EventBus.Publish(new OnPickupCollected { value = value });
        }

        #endregion

        // ────────────────────────────────
        // EVENT HANDLERS
        // ────────────────────────────────
        #region Event Handlers

        private void HandleBootstrapComplete(OnBootstrapComplete e)
        {
            // The Bootstrapper injected the pool into GameManager (R6); the bootstrap completing is
            // what guarantees it is there to read.
            _pool = GameManager.Instance != null ? GameManager.Instance.PoolManager : null;

            if (_pool == null)
            {
                Log.Error("[PickupSpawner] Bootstrap completed without a pool manager. Spawning is disabled.");
                enabled = false;
                return;
            }

            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                SpawnAt(i);
            }
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private void SpawnAt(int pointIndex)
        {
            GameObject obj = _pool.Get(_poolId);

            // Get already logged why: an unknown pool id, or an empty pool with autoExpand off.
            if (obj == null) return;

            Pickup pickup = obj.GetComponent<Pickup>();

            if (pickup == null)
            {
                Log.Error($"[PickupSpawner] The prefab of pool '{_poolId}' has no Pickup component.");
                _pool.Return(_poolId, obj);
                return;
            }

            obj.transform.SetPositionAndRotation(_spawnPoints[pointIndex].position, Quaternion.identity);
            pickup.AssignOwner(this);
            _pointByPickup.Add(pickup, pointIndex);
        }

        private void ReturnOwnedPickups()
        {
            if (_pool != null)
            {
                foreach (Pickup pickup in _pointByPickup.Keys)
                {
                    if (pickup != null) _pool.Return(_poolId, pickup.gameObject);
                }
            }

            _pointByPickup.Clear();
        }

        private bool IsConfigured(out string problem)
        {
            problem = null;

            if (string.IsNullOrWhiteSpace(_poolId))
            {
                problem = "Pool id is empty.";
            }
            else if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                problem = "No spawn points assigned.";
            }
            else if (!(_respawnDelay >= 0f))
            {
                problem = $"Respawn delay {_respawnDelay} is not zero or positive.";
            }
            else
            {
                for (int i = 0; i < _spawnPoints.Length; i++)
                {
                    if (_spawnPoints[i] != null) continue;

                    problem = $"Spawn point {i} is not assigned.";
                    break;
                }
            }

            return problem == null;
        }

        #endregion
    }
}
