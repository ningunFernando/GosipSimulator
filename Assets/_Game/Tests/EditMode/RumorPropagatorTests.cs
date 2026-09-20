using System;
using NUnit.Framework;
using GosipSimulator.Gossip;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The shape of a story: how far it gets, how much of it survives each telling, and who hears it
    /// twice. The village is the blacksmith case, son to father to a neighbour and then the elder.
    ///
    /// Every expected weight below is hand-computed from integer arithmetic that truncates toward
    /// zero, so a negative rumor shrinks the same way a positive one does. If a number here changes,
    /// Carry changed, and the two should be read together.
    /// </summary>
    public class RumorPropagatorTests
    {
        private const string SON      = "son";
        private const string SMITH    = "blacksmith";
        private const string VILLAGER = "villager";
        private const string ELDER    = "elder";

        private static SocialGraph Village(int smithToVillagerTrust = 50)
        {
            return new SocialGraph(new[]
            {
                (fromId: SON,      toId: SMITH,    trust: 90),
                (fromId: SMITH,    toId: VILLAGER, trust: smithToVillagerTrust),
                (fromId: VILLAGER, toId: ELDER,    trust: 40)
            });
        }

        private static RumorPropagator NewPropagator(SocialGraph social, int decay = 0, int maxHops = 5)
        {
            return new RumorPropagator(social, decay, maxHops);
        }

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Plan_NoTies_ReturnsNothing()
        {
            var lonely = new SocialGraph(new (string, string, int)[0]);

            Assert.IsEmpty(NewPropagator(lonely).Plan(SON, -30));
        }

        [Test]
        public void Plan_UnknownSource_ReturnsNothing()
        {
            Assert.IsEmpty(NewPropagator(Village()).Plan("stranger", -30));
        }

        [Test]
        public void Plan_ZeroInitialWeight_ReturnsNothing()
        {
            Assert.IsEmpty(NewPropagator(Village()).Plan(SON, 0),
                "A neutral action still sent a rumor around the village.");
        }

        [Test]
        public void Plan_DirectTie_CarriesTrustThenDecay()
        {
            // -30 at 90 trust with no decay is -27.
            RumorPropagator.Step first = NewPropagator(Village()).Plan(SON, -30)[0];

            Assert.AreEqual(SON, first.fromId);
            Assert.AreEqual(SMITH, first.toId);
            Assert.AreEqual(1, first.hop);
            Assert.AreEqual(-27, first.weight);
        }

        [Test]
        public void Plan_ReachesTheWholeVillageFromTheSon()
        {
            var plan = NewPropagator(Village()).Plan(SON, -30);

            Assert.AreEqual(3, plan.Count, "The story did not reach the whole village.");
            Assert.AreEqual(SMITH, plan[0].toId, "The father was not the first to hear it.");
            CollectionAssert.AreEqual(
                new[] { SMITH, VILLAGER, ELDER },
                new[] { plan[0].toId, plan[1].toId, plan[2].toId });
        }

        [Test]
        public void Plan_DecaysAcrossHops()
        {
            // Losing 40 percent per telling: -30 becomes -16, then -4, and the next hop rounds to zero.
            var plan = NewPropagator(Village(), decay: 40).Plan(SON, -30);

            Assert.AreEqual(-16, plan[0].weight);
            Assert.AreEqual(-4, plan[1].weight);
        }

        [Test]
        public void Plan_HeavyDecay_KillsTheRumorBeforeItTravels()
        {
            // At 90 percent lost per telling only the father hears it, and faintly. This is the case
            // that keeps a petty slight from becoming village-wide news.
            var plan = NewPropagator(Village(), decay: 90).Plan(SON, -30);

            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(-2, plan[0].weight);
        }

        [Test]
        public void Plan_HopThatRoundsToZero_IsNotQueued()
        {
            // The elder is two faint tellings away, so nothing reaches them and no listener ever sees
            // a rumor that moved nobody.
            var plan = NewPropagator(Village(), decay: 40).Plan(SON, -30);

            Assert.AreEqual(2, plan.Count, "A hop carrying zero weight was still published.");
            foreach (RumorPropagator.Step step in plan)
            {
                Assert.AreNotEqual(0, step.weight);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Plan_StopsAtMaxHops(int maxHops)
        {
            Assert.AreEqual(maxHops, NewPropagator(Village(), maxHops: maxHops).Plan(SON, -30).Count);
        }

        [Test]
        public void Plan_ZeroTrustCarriesNothing()
        {
            var plan = NewPropagator(Village(smithToVillagerTrust: 0)).Plan(SON, -30);

            Assert.AreEqual(1, plan.Count, "A rumor travelled through someone who is never believed.");
            Assert.AreEqual(SMITH, plan[0].toId);
        }

        [Test]
        public void Plan_NegativeWeightKeepsItsSign()
        {
            foreach (RumorPropagator.Step step in NewPropagator(Village()).Plan(SON, -30))
            {
                Assert.Less(step.weight, 0, "A theft spread as a good opinion.");
            }
        }

        [Test]
        public void Plan_EachNpcHearsItOnlyOnce()
        {
            // A cycle, with the elder telling it back to the son. The second telling must not re-charge
            // an opinion that already moved.
            var cyclic = new SocialGraph(new[]
            {
                (fromId: SON,   toId: SMITH, trust: 90),
                (fromId: SMITH, toId: ELDER, trust: 90),
                (fromId: ELDER, toId: SON,   trust: 90)
            });

            Assert.AreEqual(2, NewPropagator(cyclic).Plan(SON, -30).Count,
                "The story came back around to its source.");
        }

        [Test]
        public void Plan_StepsAreOrderedByHop()
        {
            var plan = NewPropagator(Village()).Plan(SON, -30);

            for (int i = 1; i < plan.Count; i++)
            {
                Assert.GreaterOrEqual(plan[i].hop, plan[i - 1].hop,
                    "A farther hop was delivered before a nearer one, so the story would arrive out of order.");
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Plan_BlankSource_Throws(string sourceId)
        {
            Assert.Throws<ArgumentException>(() => NewPropagator(Village()).Plan(sourceId, -30));
        }

        [Test]
        public void Constructor_NullSocialGraph_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RumorPropagator(null, 0, 5));
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void Constructor_DecayOutOfRange_Throws(int decay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RumorPropagator(Village(), decay, 5));
        }

        [Test]
        public void Constructor_NegativeMaxHops_Throws()
        {
            // ArgumentException, matching what the constructor throws today. The sibling decay check
            // throws ArgumentOutOfRangeException, so if that is ever aligned this needs widening.
            Assert.Throws<ArgumentException>(() => new RumorPropagator(Village(), 0, -1));
        }

        #endregion
    }
}
