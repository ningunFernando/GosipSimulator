using System;
using UnityEngine;

namespace GosipSimulator.Player
{
    /// <summary>
    /// Horizontal movement math, free of MonoBehaviour and Rigidbody so EditMode can test it (R5).
    /// Works on the XZ plane only: the vertical axis belongs to physics and is never touched here.
    /// </summary>
    public class PlayerMovement
    {
        private readonly float _maxSpeed;
        private readonly float _smoothTime;

        private Vector3 _smoothingVelocity;

        // ────────────────────────────────
        // INITIALIZATION
        // ────────────────────────────────
        #region Initialization

        /// <summary>
        /// The negated comparisons also reject NaN, which would otherwise spread silently into
        /// the Rigidbody velocity (A5).
        /// </summary>
        public PlayerMovement(float maxSpeed, float smoothTime)
        {
            if (!(maxSpeed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeed), maxSpeed, "Must be zero or positive.");
            }

            if (!(smoothTime > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(smoothTime), smoothTime, "Must be positive.");
            }

            _maxSpeed   = maxSpeed;
            _smoothTime = smoothTime;
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        /// <summary>
        /// The horizontal velocity for this step, with y always zero. SmoothDamp and not
        /// Lerp(a, b, speed * dt): smoothTime keeps the same feel at any frame rate (M2).
        /// Input longer than 1 is clamped, so a diagonal never outruns maxSpeed.
        /// </summary>
        public Vector3 Step(Vector3 currentVelocity, Vector2 input, float deltaTime)
        {
            Vector2 clamped = Vector2.ClampMagnitude(input, 1f);

            Vector3 target  = new Vector3(clamped.x, 0f, clamped.y) * _maxSpeed;
            Vector3 current = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

            return Vector3.SmoothDamp(current, target, ref _smoothingVelocity, _smoothTime, Mathf.Infinity, deltaTime);
        }

        #endregion
    }
}
