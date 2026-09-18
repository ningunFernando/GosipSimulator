using NUnit.Framework;
using GosipSimulator.Core.State;
using GosipSimulator.Debug;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The HUD text without a panel. Whether the events actually reach the HUD in a running game
    /// is proven end to end in BootstrapSequenceTests.
    /// </summary>
    public class DebugHudModelTests
    {
        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Text_BeforeAnyEvent_ShowsPendingAndUnknown()
        {
            var model = new DebugHudModel();

            StringAssert.Contains("Bootstrap: pending", model.Text);
            StringAssert.Contains("State: unknown", model.Text);
        }

        [Test]
        public void MarkBootstrapComplete_ShowsComplete()
        {
            var model = new DebugHudModel();

            model.MarkBootstrapComplete();

            StringAssert.Contains("Bootstrap: complete", model.Text);
        }

        [Test]
        public void SetState_Twice_ShowsLatestState()
        {
            var model = new DebugHudModel();

            model.SetState(GameState.Menu);
            model.SetState(GameState.Play);

            StringAssert.Contains("State: Play", model.Text);
            StringAssert.DoesNotContain("Menu", model.Text);
        }

        [Test]
        public void SetState_Bootstrap_IsNotReportedAsUnknown()
        {
            // Bootstrap is the first enum value, so a non-nullable field would hold it by default
            // and a HUD that never received an event would claim a state it was never told.
            var model = new DebugHudModel();

            model.SetState(GameState.Bootstrap);

            StringAssert.Contains("State: Bootstrap", model.Text);
        }

        [Test]
        public void Text_BeforeAnyProgress_ShowsUnknownCurrency()
        {
            StringAssert.Contains("Currency: unknown", new DebugHudModel().Text);
        }

        [Test]
        public void SetCurrency_Twice_ShowsLatestValue()
        {
            var model = new DebugHudModel();

            model.SetCurrency(3);
            model.SetCurrency(0);

            // Zero is a real balance, not "unknown".
            StringAssert.Contains("Currency: 0", model.Text);
        }

        #endregion
    }
}
