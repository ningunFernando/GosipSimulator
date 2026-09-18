using System;
using NUnit.Framework;
using UnityEngine;
using GosipSimulator.Player;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The movement math without a Rigidbody. The frame rate test is the one that matters: the
    /// reference smoothed with Lerp(a, b, speed * dt), which feels different at 30 and 144 fps (M2).
    /// </summary>
    public class PlayerMovementTests
    {
        private const float MaxSpeed   = 5f;
        private const float SmoothTime = 0.1f;

        private static readonly Vector2 Forward = new Vector2(0f, 1f);

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [TestCase(-1f,       0.1f)]
        [TestCase(float.NaN, 0.1f)]
        [TestCase(5f,        0f)]
        [TestCase(5f,        float.NaN)]
        public void Constructor_InvalidValues_Throws(float maxSpeed, float smoothTime)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerMovement(maxSpeed, smoothTime));
        }

        [Test]
        public void Step_FullInputFromRest_ReachesMaxSpeed()
        {
            Vector3 velocity = Simulate(Vector3.zero, Forward, seconds: 1f, fps: 60);

            Assert.AreEqual(MaxSpeed, velocity.z, 0.01f);
            Assert.AreEqual(0f, velocity.x, 0.0001f);
        }

        [Test]
        public void Step_ZeroInput_StopsFromMaxSpeed()
        {
            Vector3 velocity = Simulate(new Vector3(MaxSpeed, 0f, 0f), Vector2.zero, seconds: 1f, fps: 60);

            Assert.Less(velocity.magnitude, 0.01f);
        }

        [Test]
        public void Step_DiagonalInput_IsClampedToMaxSpeed()
        {
            // Full diagonal from two raw axes is about 1.41 long and would outrun maxSpeed.
            Vector3 velocity = Simulate(Vector3.zero, new Vector2(1f, 1f), seconds: 1f, fps: 60);

            Assert.AreEqual(MaxSpeed, velocity.magnitude, 0.01f);
        }

        [Test]
        public void Step_CurrentVerticalVelocity_IsNotReturned()
        {
            var movement = new PlayerMovement(MaxSpeed, SmoothTime);

            Vector3 velocity = movement.Step(new Vector3(0f, -9f, 0f), Vector2.zero, 1f / 50f);

            // PlayerMover keeps the Rigidbody's own y; a non-zero y here would fight gravity.
            Assert.AreEqual(0f, velocity.y);
        }

        [Test]
        public void Step_SameDuration_FeelsTheSameAt30And120Fps()
        {
            // Compared mid-transition, where frame rate dependence shows most. With SmoothDamp the
            // two runs differ by about 0.012 m/s here; the reference's Lerp(v, max, 20 * dt) differs
            // by about 0.38 m/s (4.81 against 4.44), so the tolerance tells the two apart.
            Vector3 at30  = Simulate(Vector3.zero, Forward, seconds: SmoothTime, fps: 30);
            Vector3 at120 = Simulate(Vector3.zero, Forward, seconds: SmoothTime, fps: 120);

            Assert.AreEqual(at120.z, at30.z, 0.05f);
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private static Vector3 Simulate(Vector3 startVelocity, Vector2 input, float seconds, int fps)
        {
            var movement = new PlayerMovement(MaxSpeed, SmoothTime);

            Vector3 velocity  = startVelocity;
            int     steps     = Mathf.RoundToInt(seconds * fps);
            float   deltaTime = 1f / fps;

            for (int i = 0; i < steps; i++)
            {
                velocity = movement.Step(velocity, input, deltaTime);
            }

            return velocity;
        }

        #endregion
    }
}
