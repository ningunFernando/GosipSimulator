using NUnit.Framework;
using GosipSimulator.Core.State;
using GosipSimulator.Debug;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The HUD text without a panel. Whether the events actually reach the HUD in a running game
    /// is proven end to end in BootstrapSequenceTests and, for the village, in HudFlowTests.
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

        [Test]
        public void Text_BeforeTheVillageSaysAnything_ShowsNoneAndUnknown()
        {
            string text = new DebugHudModel().Text;

            StringAssert.Contains("Opinions: none", text);
            StringAssert.Contains("Rumors: none", text);
            StringAssert.Contains("Shop: unknown", text);
            StringAssert.Contains("Last purchase: none", text);
        }

        [Test]
        public void SetOpinion_ShowsWhoThinksWhatAndWhy()
        {
            var model = new DebugHudModel();

            model.SetOpinion("son", "player", -10, "robbery");

            StringAssert.Contains("son -> player: -10 (robbery)", model.Text);
            StringAssert.DoesNotContain("Opinions: none", model.Text);
        }

        [Test]
        public void SetOpinion_Positive_CarriesAPlusSign()
        {
            var model = new DebugHudModel();

            model.SetOpinion("son", "player", 20, "help");

            StringAssert.Contains("son -> player: +20 (help)", model.Text);
        }

        [Test]
        public void SetOpinion_Twice_ShowsOnlyTheLatest()
        {
            var model = new DebugHudModel();

            model.SetOpinion("blacksmith", "player", -5, "robbery");
            model.SetOpinion("blacksmith", "player", -15, "robbery");

            StringAssert.Contains("blacksmith -> player: -15", model.Text);
            StringAssert.DoesNotContain(": -5 (", model.Text);
        }

        [Test]
        public void SetOpinion_BackToZero_RemovesTheLine()
        {
            // Sparse, like Gossip and Save: neutral is the absence of an opinion, not a zero.
            var model = new DebugHudModel();

            model.SetOpinion("son", "player", -10, "robbery");
            model.SetOpinion("son", "player", 0, "help");

            StringAssert.Contains("Opinions: none", model.Text);
        }

        [Test]
        public void Opinions_AreListedInIdOrder_NotArrivalOrder()
        {
            // Arrival order would reshuffle the list every time a rumor landed somewhere new.
            var model = new DebugHudModel();

            model.SetOpinion("villager", "player", -1, "robbery");
            model.SetOpinion("blacksmith", "player", -5, "robbery");
            model.SetOpinion("son", "player", -10, "robbery");

            string text = model.Text;

            Assert.Less(text.IndexOf("blacksmith ->"), text.IndexOf("son ->"));
            Assert.Less(text.IndexOf("son ->"), text.IndexOf("villager ->"));
        }

        [Test]
        public void SetOpinion_WithAnEmptyId_IsIgnored()
        {
            var model = new DebugHudModel();

            model.SetOpinion("", "player", -10, "robbery");

            StringAssert.Contains("Opinions: none", model.Text);
        }

        [Test]
        public void RestoreOpinions_ShowsThemAsSaved()
        {
            var model = new DebugHudModel();

            model.RestoreOpinions(new[] { "elder" }, new[] { "player" }, new[] { -77 });

            StringAssert.Contains("elder -> player: -77 (saved)", model.Text);
        }

        [Test]
        public void RestoreOpinions_WithNullArrays_IsAnEmptySave()
        {
            // What a fresh save publishes. Not an error, and nothing to show.
            var model = new DebugHudModel();

            Assert.DoesNotThrow(() => model.RestoreOpinions(null, null, null));
            StringAssert.Contains("Opinions: none", model.Text);
        }

        [Test]
        public void RestoreOpinions_WithMismatchedArrays_ShowsNothing()
        {
            var model = new DebugHudModel();

            model.RestoreOpinions(new[] { "elder", "son" }, new[] { "player" }, new[] { -77 });

            StringAssert.Contains("Opinions: none", model.Text);
        }

        [Test]
        public void AddRumor_ShowsTheHop()
        {
            var model = new DebugHudModel();

            model.AddRumor("robbery", "son", "blacksmith", 1, -5);

            StringAssert.Contains("son -> blacksmith: robbery, hop 1, -5", model.Text);
        }

        [Test]
        public void AddRumor_KeepsOnlyTheMostRecent()
        {
            var model = new DebugHudModel();

            for (int hop = 1; hop <= DebugHudModel.RUMOR_LINES + 1; hop++)
            {
                model.AddRumor("robbery", "a", "b", hop, -1);
            }

            StringAssert.DoesNotContain("hop 1,", model.Text, "The oldest rumor should have scrolled off.");
            StringAssert.Contains($"hop {DebugHudModel.RUMOR_LINES + 1},", model.Text);
        }

        [Test]
        public void SetShopTerms_ShowsPriceAndPercent()
        {
            var model = new DebugHudModel();

            model.SetShopTerms("blacksmith", 6, 110, false);

            StringAssert.Contains("blacksmith: 6 (110%)", model.Text);
        }

        [Test]
        public void SetShopTerms_Refusing_SaysSoAndWhatItWouldCost()
        {
            var model = new DebugHudModel();

            model.SetShopTerms("blacksmith", 8, 160, true);

            StringAssert.Contains("blacksmith: refuses to sell (would be 8 at 160%)", model.Text);
        }

        [Test]
        public void RecordPurchaseSettled_Paid_SaysWhatWasBought()
        {
            var model = new DebugHudModel();

            model.RecordPurchaseSettled("blacksmith", "horseshoe", 6, true);

            StringAssert.Contains("Last purchase: bought horseshoe from blacksmith for 6", model.Text);
        }

        [Test]
        public void RecordPurchaseSettled_Unpaid_SaysItWasUnaffordable()
        {
            var model = new DebugHudModel();

            model.RecordPurchaseSettled("blacksmith", "horseshoe", 6, false);

            StringAssert.Contains("Last purchase: could not afford horseshoe at 6", model.Text);
        }

        [Test]
        public void RecordPurchaseRefused_SaysWhoAndWhy()
        {
            var model = new DebugHudModel();

            model.RecordPurchaseRefused("blacksmith", "horseshoe", -40);

            StringAssert.Contains("Last purchase: blacksmith refused to sell horseshoe (opinion -40)", model.Text);
        }

        #endregion
    }
}
