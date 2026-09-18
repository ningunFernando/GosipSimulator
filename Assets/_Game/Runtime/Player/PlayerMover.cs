using UnityEngine;
using GosipSimulator.Core;

namespace GosipSimulator.Player
{
    /// <summary>
    /// Adapter between PlayerInputReader, PlayerMovement and the Rigidbody. Moves in FixedUpdate,
    /// so each physics step is also one smoothing step, and only writes the horizontal velocity:
    /// gravity keeps the vertical axis.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMover : MonoBehaviour
    {
        private const float MIN_SMOOTH_TIME = 0.01f;

        // ────────────────────────────────
        // INSPECTOR
        // ────────────────────────────────
        #region Inspector

        [Header("References")]
        [Tooltip("Input source for this player. Assigned in the Inspector, never looked up at runtime (R6).")]
        [SerializeField] private PlayerInputReader _input;

        [Header("Movement")]
        [Tooltip("Top horizontal speed, in meters per second.")]
        [SerializeField] private float _maxSpeed   = 5f;
        [Tooltip("Roughly the seconds it takes to close most of the gap to the target speed. Lower feels snappier.")]
        [SerializeField] private float _smoothTime = 0.1f;

        #endregion

        private Rigidbody      _body;
        private PlayerMovement _movement;

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void OnValidate()
        {
            // The Inspector stores a negative speed or a zero smooth time without complaint (R8).
            _maxSpeed   = Mathf.Max(0f, _maxSpeed);
            _smoothTime = Mathf.Max(MIN_SMOOTH_TIME, _smoothTime);
        }

        private void Awake()
        {
            // Checked again here because OnValidate never runs in a build (R8). Disabled instead of
            // left half-working, where FixedUpdate would hit a null on every physics step (R9).
            if (_input == null)
            {
                Log.Error("[PlayerMover] PlayerInputReader not assigned. Movement is disabled.");
                enabled = false;
                return;
            }

            if (!(_maxSpeed >= 0f) || !(_smoothTime >= MIN_SMOOTH_TIME))
            {
                Log.Error($"[PlayerMover] Invalid movement values (maxSpeed {_maxSpeed}, smoothTime {_smoothTime}). Movement is disabled.");
                enabled = false;
                return;
            }

            _body     = GetComponent<Rigidbody>();
            _movement = new PlayerMovement(_maxSpeed, _smoothTime);
        }

        private void FixedUpdate()
        {
            Vector3 velocity   = _body.linearVelocity;
            Vector3 horizontal = _movement.Step(velocity, _input.Move, Time.fixedDeltaTime);

            _body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        #endregion
    }
}
