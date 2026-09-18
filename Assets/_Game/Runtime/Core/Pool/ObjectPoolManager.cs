using System;
using System.Collections.Generic;
using UnityEngine;

namespace GosipSimulator.Core.Pool
{
    /// <summary>
    /// Pools GameObjects by string id. Get is the only place that activates anything, which is
    /// what keeps the dequeue path and the auto-expand path from handing back different states
    /// of the same object (C1).
    /// </summary>
    public class ObjectPoolManager : MonoBehaviour
    {
        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Pool Configurations")]
        [Tooltip("Pools created by InitializePools. An empty list is valid.")]
        [SerializeField] private List<PoolConfig> _poolConfigs = new List<PoolConfig>();

        #endregion

        private readonly Dictionary<string, Queue<GameObject>>   _pools
            = new Dictionary<string, Queue<GameObject>>();

        private readonly Dictionary<string, HashSet<GameObject>> _active
            = new Dictionary<string, HashSet<GameObject>>();

        private readonly Dictionary<string, PoolConfig>          _configs
            = new Dictionary<string, PoolConfig>();

        private readonly Dictionary<string, Transform>           _containers
            = new Dictionary<string, Transform>();

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            ValidateConfigs();
        }

        /// <summary>
        /// Clamping here is an editing aid only (R8). It runs in the Editor, never in a build,
        /// and a value already serialized outside the range still loads unchanged: Awake above
        /// is the check that holds at runtime.
        /// </summary>
        private void OnValidate()
        {
            for (int i = 0; i < _poolConfigs.Count; i++)
            {
                PoolConfig config = _poolConfigs[i];

                if (config == null) continue;
                if (config.initialSize < 0) config.initialSize = 0;
            }
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public void InitializePools()
        {
            foreach (PoolConfig config in _poolConfigs)
            {
                CreatePool(config);
            }

            Log.Trace($"[ObjectPoolManager] {_pools.Count} pools initialized.");
        }

        public GameObject Get(string poolId)
        {
            if (!_pools.TryGetValue(poolId, out Queue<GameObject> pool))
            {
                Log.Error($"[ObjectPoolManager] Pool '{poolId}' does not exist.");
                return null;
            }

            GameObject obj;

            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else if (_configs[poolId].autoExpand)
            {
                Log.Warn($"[ObjectPoolManager] Pool '{poolId}' empty. Expanding.");
                obj = CreateInstance(_configs[poolId]);
            }
            else
            {
                Log.Warn($"[ObjectPoolManager] Pool '{poolId}' empty and autoExpand is off.");
                return null;
            }

            // Single active exit. The auto-expand branch used to return here directly and
            // handed back the object still inactive (C1).
            obj.SetActive(true);
            _active[poolId].Add(obj);
            obj.GetComponent<IPoolable>()?.OnSpawn();

            return obj;
        }

        public void Return(string poolId, GameObject obj)
        {
            if (!_active.TryGetValue(poolId, out HashSet<GameObject> active))
            {
                Log.Warn($"[ObjectPoolManager] Return called for unknown pool '{poolId}'. Destroying the object.");
                Destroy(obj);
                return;
            }

            // Not in the active set means it was already returned once (M3).
            if (!active.Remove(obj))
            {
                Log.Warn($"[ObjectPoolManager] Double return of '{obj.name}' to pool '{poolId}' ignored.");
                return;
            }

            obj.GetComponent<IPoolable>()?.OnDespawn();
            obj.SetActive(false);
            obj.transform.SetParent(_containers[poolId]);
            _pools[poolId].Enqueue(obj);
        }

        public void ReturnAll(string poolId)
        {
            if (!_containers.TryGetValue(poolId, out Transform container)) return;

            // Backwards on purpose: Return reparents, which reorders siblings, so walking the
            // hierarchy forwards while mutating it skips elements (M4).
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                GameObject child = container.GetChild(i).gameObject;

                if (child.activeSelf) Return(poolId, child);
            }
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        /// <summary>
        /// Rejects a bad configuration before any pool exists, instead of discovering it halfway
        /// through InitializePools and leaving some pools built and others missing (R8, R9).
        /// </summary>
        private void ValidateConfigs()
        {
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < _poolConfigs.Count; i++)
            {
                PoolConfig config = _poolConfigs[i];

                if (config == null)
                {
                    throw new InvalidOperationException($"[ObjectPoolManager] Pool config at index {i} is null.");
                }

                if (string.IsNullOrWhiteSpace(config.poolId))
                {
                    throw new InvalidOperationException($"[ObjectPoolManager] Pool config at index {i} has an empty poolId.");
                }

                if (config.prefab == null)
                {
                    throw new InvalidOperationException($"[ObjectPoolManager] Pool '{config.poolId}' has no prefab assigned.");
                }

                if (!seen.Add(config.poolId))
                {
                    throw new InvalidOperationException($"[ObjectPoolManager] Duplicate poolId '{config.poolId}'.");
                }
            }
        }

        private void CreatePool(PoolConfig config)
        {
            GameObject containerObject = new GameObject($"Pool_{config.poolId}");
            containerObject.transform.SetParent(transform);

            _pools[config.poolId]      = new Queue<GameObject>();
            _active[config.poolId]     = new HashSet<GameObject>();
            _configs[config.poolId]    = config;
            _containers[config.poolId] = containerObject.transform;

            for (int i = 0; i < config.initialSize; i++)
            {
                _pools[config.poolId].Enqueue(CreateInstance(config));
            }

            Log.Trace($"[ObjectPoolManager] Pool '{config.poolId}' created with {config.initialSize} objects.");
        }

        private GameObject CreateInstance(PoolConfig config)
        {
            GameObject obj = Instantiate(config.prefab, _containers[config.poolId]);

            // This method only ever feeds the queue; Get is what activates.
            obj.SetActive(false);

            return obj;
        }

        #endregion
    }
}
