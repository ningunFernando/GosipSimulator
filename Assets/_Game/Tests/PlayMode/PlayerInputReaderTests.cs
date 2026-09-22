using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GosipSimulator.Player;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The reader's failure path. Reading real input is covered end to end in PlayerMoverTests and
    /// PauseFlowTests. PlayMode because Awake and OnEnable only run there.
    /// </summary>
    [IsolatedSave]
    public class PlayerInputReaderTests
    {
        private static readonly Regex NoMoveAction =
            new Regex(@"^\[PlayerInputReader\] Move action not assigned");
        private static readonly Regex NoPauseAction =
            new Regex(@"^\[PlayerInputReader\] Pause action not assigned");

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Awake_WithoutActions_LogsErrorsAndDisables()
        {
            // Created inactive so Awake waits for SetActive, after the expectations are in place.
            var host = new GameObject("ReaderWithoutActions");
            host.SetActive(false);
            PlayerInputReader reader = host.AddComponent<PlayerInputReader>();

            LogAssert.Expect(LogType.Error, NoMoveAction);
            LogAssert.Expect(LogType.Error, NoPauseAction);

            // Disabling in Awake makes Unity call OnDisable immediately, before any OnEnable. The first
            // version dereferenced the missing action there and threw a NullReferenceException right
            // after the error; any exception from OnEnable or OnDisable fails this test as unexpected.
            host.SetActive(true);

            Assert.IsFalse(reader.enabled, "The reader stayed enabled without its actions.");
            Assert.AreEqual(Vector2.zero, reader.Move);

            Object.Destroy(host);
        }

        #endregion
    }
}
