using System;
using UnityEngine;

namespace GosipSimulator.Core.Pool
{
    /// <summary>
    /// Inspector-facing description of one pool. A serializable class, not a struct, so
    /// OnValidate on the owner can clamp its fields in place.
    /// </summary>
    [Serializable]
    public class PoolConfig
    {
        [Tooltip("Unique id used to request and return objects from this pool.")]
        public string poolId;

        [Tooltip("Prefab instantiated to fill the pool.")]
        public GameObject prefab;

        [Tooltip("Objects created up front when the pool is initialized.")]
        public int initialSize;

        [Tooltip("Create a new object on demand when the pool runs empty. If off, Get returns null instead.")]
        public bool autoExpand = true;
    }
}
