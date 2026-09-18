using System.Reflection;
using NUnit.Framework;
using GosipSimulator.Core;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The entry scene rule on its own. Whether Unity actually runs the guard, and what it logs, is
    /// covered end to end in BootstrapSequenceTests.
    /// </summary>
    public class BootstrapperTests
    {
        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [TestCase(1,  "Scene_Game",      ExpectedResult = true)]
        [TestCase(0,  "Scene_Bootstrap", ExpectedResult = false)]
        [TestCase(-1, "Sandbox",         ExpectedResult = false)]
        // The case that bit: the Test Runner's scene reports a positive build index during a run.
        [TestCase(2,  "InitTestScene934af14d-67a4-49eb-94cd-39c3f1f0f219", ExpectedResult = false)]
        public bool DependsOnBootstrap_ForScene_MatchesEntryRule(int buildIndex, string sceneName)
        {
            return InvokeDependsOnBootstrap(buildIndex, sceneName);
        }

        #endregion

        // ────────────────────────────────
        // HELPERS
        // ────────────────────────────────
        #region Helpers

        private static bool InvokeDependsOnBootstrap(int buildIndex, string sceneName)
        {
            MethodInfo method = typeof(Bootstrapper)
                .GetMethod("DependsOnBootstrap", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method,
                "Bootstrapper.DependsOnBootstrap was renamed or removed. Update this test seam.");

            return (bool)method.Invoke(null, new object[] { buildIndex, sceneName });
        }

        #endregion
    }
}
