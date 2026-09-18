using UnityEngine;
using GosipSimulator.Core;
using GosipSimulator.Core.Pool;

namespace GosipSimulator.Pickups
{
    /// <summary>
    /// A pooled collectible. It only knows its value and the spawner that placed it: the spawner is
    /// assigned on every spawn and cleared on every return, so a reused pickup never reports to the
    /// owner of a previous use (R6).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour, IPoolable
    {
        private const string PLAYER_TAG = "Player";
        private const int    MIN_VALUE  = 1;

        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("Reward")]
        [Tooltip("Currency granted when the player collects this pickup. At least 1.")]
        [SerializeField] private int _value = 1;

        #endregion

        private PickupSpawner _owner;
        private bool          _collected;

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public int Value => _value;

        public void OnSpawn()
        {
            _collected = false;
        }

        public void OnDespawn()
        {
            _owner = null;
        }

        internal void AssignOwner(PickupSpawner owner)
        {
            _owner = owner;
        }

        #endregion

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void OnValidate()
        {
            _value = Mathf.Max(MIN_VALUE, _value);
        }

        private void Awake()
        {
            // Checked again here because OnValidate never runs in a build (R8). A value below 1 would
            // make ProgressService.Earn throw at collection time, far from the prefab that caused it,
            // so the fallback is a valid value plus an error that names the cause (R9).
            if (_value < MIN_VALUE)
            {
                Log.Error($"[Pickup] Value {_value} on '{name}' is below {MIN_VALUE}. Using {MIN_VALUE}.");
                _value = MIN_VALUE;
            }

            Collider trigger = GetComponent<Collider>();

            if (!trigger.isTrigger)
            {
                Log.Error($"[Pickup] Collider on '{name}' is not a trigger, so it would block the player. Making it one.");
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Trigger messages reach disabled components too, so the guards cannot rely on enabled.
            if (_collected || _owner == null) return;

            Rigidbody body = other.attachedRigidbody;

            if (body == null || !body.CompareTag(PLAYER_TAG)) return;

            // Set before reporting: two colliders of the player can enter in the same physics step.
            _collected = true;
            _owner.Collect(this);
        }

        #endregion
    }
}
