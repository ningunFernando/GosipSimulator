using System;
using NUnit.Framework;
using GosipSimulator.Shop;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The blacksmith's arithmetic. The cases that matter are the ones a player would notice: one
    /// rumor has to cost something even at a small base price, a friend cannot be paid to take the
    /// item, and the door closes at a threshold rather than somewhere near it.
    ///
    /// The numbers mirror the scene: a base price of 5, 2% per point, a floor of 50% and refusal at
    /// -30, so the -5 a single robbery leaves the blacksmith at reads as 110%.
    /// </summary>
    public class PricingPolicyTests
    {
        private const int BasePrice       = 5;
        private const int PercentPerPoint = 2;
        private const int MinPercent      = 50;
        private const int RefuseAtOrBelow = -30;

        private PricingPolicy _policy;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            _policy = new PricingPolicy(BasePrice, PercentPerPoint, MinPercent, RefuseAtOrBelow);
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [TestCase(0)]
        [TestCase(-1)]
        public void ABasePriceBelowOne_IsRejected(int basePrice)
        {
            // A price of zero is something Save cannot take payment for, so it never gets that far.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PricingPolicy(basePrice, PercentPerPoint, MinPercent, RefuseAtOrBelow));
        }

        [Test]
        public void ANegativeRate_IsRejected()
        {
            // It would make the most hated customer in the village the one with the best price.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PricingPolicy(BasePrice, -1, MinPercent, RefuseAtOrBelow));
        }

        [TestCase(0)]
        [TestCase(101)]
        public void AFloorOutsideOneToAHundred_IsRejected(int minPercent)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PricingPolicy(BasePrice, PercentPerPoint, minPercent, RefuseAtOrBelow));
        }

        [Test]
        public void AStranger_PaysTheBasePrice()
        {
            ShopTerms terms = _policy.For(0);

            Assert.AreEqual(100, terms.Percent);
            Assert.AreEqual(BasePrice, terms.Price);
            Assert.IsFalse(terms.Refuses);
        }

        [Test]
        public void OneRobberyWorthOfRumor_CostsMore()
        {
            // The -5 the README says a robbery leaves the blacksmith at: 110% of 5 is 5.5, which
            // rounds up to 6. Rounding down would have left the price at 5 and hidden the rumor.
            ShopTerms terms = _policy.For(-5);

            Assert.AreEqual(110, terms.Percent);
            Assert.AreEqual(6, terms.Price);
        }

        [Test]
        public void AGoodWord_CostsLess()
        {
            // The +10 helping at the well leaves the blacksmith at.
            ShopTerms terms = _policy.For(10);

            Assert.AreEqual(80, terms.Percent);
            Assert.AreEqual(4, terms.Price);
        }

        [Test]
        public void RoundingIsInTheShopsFavour_EvenOnADiscount()
        {
            // 90% of 5 is 4.5. A discount that small does not survive the rounding, on purpose: the
            // rule is one direction for everything rather than whichever direction flatters the player.
            Assert.AreEqual(5, _policy.For(5).Price);
        }

        [Test]
        public void TheDiscount_StopsAtTheFloor()
        {
            ShopTerms terms = _policy.For(100);

            Assert.AreEqual(MinPercent, terms.Percent);
            Assert.AreEqual(3, terms.Price, "50% of 5 is 2.5, which rounds up to 3.");
        }

        [Test]
        public void ThePrice_NeverFallsBelowOne()
        {
            // A 1% floor on a price of 1 is 0.01. Rounding up keeps it at one, which is what lets
            // every approved sale reach TrySpend without being thrown back as free.
            var cheap = new PricingPolicy(1, 100, 1, RefuseAtOrBelow);

            Assert.AreEqual(1, cheap.For(100).Price);
        }

        [Test]
        public void TheDoorCloses_ExactlyAtTheThreshold()
        {
            Assert.IsTrue(_policy.For(RefuseAtOrBelow).Refuses, "The threshold is inclusive.");
            Assert.IsFalse(_policy.For(RefuseAtOrBelow + 1).Refuses, "One point above it still buys.");
        }

        [Test]
        public void BelowTheThreshold_TheDoorStaysClosed()
        {
            Assert.IsTrue(_policy.For(-100).Refuses);
        }

        [Test]
        public void ARefusal_StillCarriesThePriceItWouldHaveBeen()
        {
            // So the HUD can say what the door is costing you. -30 at 2% a point is 160%, and 160%
            // of 5 is exactly 8.
            ShopTerms terms = _policy.For(-30);

            Assert.IsTrue(terms.Refuses);
            Assert.AreEqual(160, terms.Percent);
            Assert.AreEqual(8, terms.Price);
        }

        [Test]
        public void AZeroRate_IgnoresTheOpinionForPrice_ButNotForRefusal()
        {
            // Pricing and refusing are separate consequences. A shop can choose not to haggle and
            // still refuse to serve a thief.
            var flat = new PricingPolicy(BasePrice, 0, MinPercent, RefuseAtOrBelow);

            Assert.AreEqual(BasePrice, flat.For(-29).Price);
            Assert.AreEqual(BasePrice, flat.For(90).Price);
            Assert.IsTrue(flat.For(-30).Refuses);
        }

        [Test]
        public void AThresholdBelowEveryOpinion_NeverRefuses()
        {
            var patient = new PricingPolicy(BasePrice, PercentPerPoint, MinPercent, -101);

            Assert.IsFalse(patient.For(-100).Refuses);
        }

        [Test]
        public void ExtremeInputs_DoNotOverflow()
        {
            // Gossip clamps opinions to its own range, but this class does not know that range.
            var steep = new PricingPolicy(int.MaxValue, 1000, MinPercent, int.MinValue);

            ShopTerms terms = default;

            Assert.DoesNotThrow(() => terms = steep.For(int.MinValue));
            Assert.AreEqual(int.MaxValue, terms.Price, "The price should saturate, not wrap around negative.");
            Assert.AreEqual(int.MaxValue, terms.Percent);
        }

        [Test]
        public void TheSameOpinion_AlwaysGivesTheSameTerms()
        {
            // Terms are compared by value in the tests and in the log, so two computations of the
            // same opinion have to be equal rather than merely look alike.
            Assert.AreEqual(_policy.For(-7), _policy.For(-7));
            Assert.AreNotEqual(_policy.For(-7), _policy.For(-8));
        }

        #endregion
    }
}
