using System;
using System.Collections.Generic;
using NUnit.Framework;
using GosipSimulator.Core;
using GosipSimulator.Gossip;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// The whole blacksmith case without a scene: the son sees a theft, his opinion of the player moves
    /// at once, and the story reaches his father one hop delay later. The two sinks stand in for
    /// SaveSystem, Shopkeeper and the HUD, which are the real subscribers; without them every publish
    /// would go into an empty bus and warn about it.
    /// </summary>
    public class GossipServiceTests
    {
        private const string PLAYER = "player";
        private const string SON    = "son";
        private const string SMITH  = "blacksmith";
        private const string ELDER  = "elder";
        private const string STEAL  = "steal";

        private const float HOP_DELAY = 2f;

        private readonly List<OnRelationshipChanged> _changed = new List<OnRelationshipChanged>();
        private readonly List<OnRumorSpread>         _spread  = new List<OnRumorSpread>();

        private Action<OnRelationshipChanged> _changedSink;
        private Action<OnRumorSpread>         _spreadSink;

        private RelationshipGraph _opinions;
        private GossipService     _gossip;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAllSubscriptions();
            _changed.Clear();
            _spread.Clear();

            _changedSink = e => _changed.Add(e);
            _spreadSink  = e => _spread.Add(e);

            EventBus.Subscribe(_changedSink);
            EventBus.Subscribe(_spreadSink);

            _opinions = new RelationshipGraph(-100, 100);
            _gossip   = NewService(sonToSmithTrust: 90);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_changedSink);
            EventBus.Unsubscribe(_spreadSink);
            EventBus.ClearAllSubscriptions();
        }

        private GossipService NewService(int sonToSmithTrust = 90, int smithToElderTrust = 100)
        {
            var social = new SocialGraph(new[]
            {
                (fromId: SON,   toId: SMITH, trust: sonToSmithTrust),
                (fromId: SMITH, toId: ELDER, trust: smithToElderTrust)
            });

            return new GossipService(_opinions, new RumorPropagator(social, 0, 5), HOP_DELAY);
        }

        private void WitnessTheft()
        {
            Assert.IsTrue(_gossip.WitnessAction(STEAL, PLAYER, SON, -30));
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void WitnessAction_MovesTheWitnessOpinionAboutTheActor()
        {
            WitnessTheft();

            Assert.AreEqual(-30, _opinions.Get(SON, PLAYER));
        }

        [Test]
        public void WitnessAction_LeavesTheReverseEdgeAlone()
        {
            WitnessTheft();

            Assert.AreEqual(0, _opinions.Get(PLAYER, SON),
                "The opinion ran backwards, so being seen stealing made the player dislike the son.");
        }

        [Test]
        public void WitnessAction_PublishesRelationshipChangedOnce()
        {
            WitnessTheft();

            Assert.AreEqual(1, _changed.Count);
            Assert.AreEqual(SON, _changed[0].npcId);
            Assert.AreEqual(PLAYER, _changed[0].aboutId);
            Assert.AreEqual(0, _changed[0].previous);
            Assert.AreEqual(-30, _changed[0].current);
            Assert.AreEqual(STEAL, _changed[0].reason);
        }

        [Test]
        public void WitnessAction_QueuesTheRumorWithoutSpreadingItYet()
        {
            WitnessTheft();

            Assert.AreEqual(2, _gossip.PendingCount);
            Assert.IsEmpty(_spread, "The story travelled before any game time passed.");
            Assert.AreEqual(0, _opinions.Get(SMITH, PLAYER), "The father already knows.");
        }

        [Test]
        public void WitnessAction_ZeroDelta_ChangesNothingAndPublishesNothing()
        {
            Assert.IsTrue(_gossip.WitnessAction("glance", PLAYER, SON, 0));

            Assert.IsEmpty(_changed);
            Assert.IsEmpty(_spread);
            Assert.AreEqual(0, _gossip.PendingCount, "A neutral action queued a rumor.");
        }

        [Test]
        public void WitnessAction_AlreadyAtTheClamp_PublishesNothing()
        {
            _opinions.Set(SON, PLAYER, -100);
            _changed.Clear();

            WitnessTheft();

            Assert.IsEmpty(_changed, "A clamped opinion published a change that did not happen.");
        }

        [Test]
        public void WitnessAction_ActorIsTheirOwnWitness_IsIgnored()
        {
            Assert.IsFalse(_gossip.WitnessAction(STEAL, PLAYER, PLAYER, -30),
                "An NPC witnessing their own action was accepted instead of being ignored.");

            Assert.IsEmpty(_changed);
            Assert.AreEqual(0, _gossip.PendingCount);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void WitnessAction_BlankActionId_Throws(string actionId)
        {
            Assert.Throws<ArgumentException>(() => _gossip.WitnessAction(actionId, PLAYER, SON, -30));
        }

        [Test]
        public void Tick_BeforeTheHopDelay_SpreadsNothing()
        {
            WitnessTheft();

            _gossip.Tick(HOP_DELAY - 0.01f);

            Assert.IsEmpty(_spread);
            Assert.AreEqual(2, _gossip.PendingCount);
        }

        [Test]
        public void Tick_AfterTheHopDelay_TheFatherHearsIt()
        {
            WitnessTheft();

            _gossip.Tick(HOP_DELAY);

            Assert.AreEqual(-27, _opinions.Get(SMITH, PLAYER),
                "The blacksmith never formed an opinion, so he would still charge full price.");
            Assert.AreEqual(1, _gossip.PendingCount, "Both hops were delivered on the first interval.");
        }

        [Test]
        public void Tick_PublishesRumorSpreadWithHopAndWeight()
        {
            WitnessTheft();

            _gossip.Tick(HOP_DELAY);

            Assert.AreEqual(1, _spread.Count);
            Assert.AreEqual(SON, _spread[0].fromId);
            Assert.AreEqual(SMITH, _spread[0].toId);
            Assert.AreEqual(1, _spread[0].hop);
            Assert.AreEqual(-27, _spread[0].weight);
            Assert.AreEqual(STEAL, _spread[0].actionId);
        }

        [Test]
        public void Tick_PublishesRelationshipChangedForTheReceiver()
        {
            WitnessTheft();
            _changed.Clear();

            _gossip.Tick(HOP_DELAY);

            Assert.AreEqual(1, _changed.Count, "The father's opinion moved without anybody being told.");
            Assert.AreEqual(SMITH, _changed[0].npcId);
            Assert.AreEqual(PLAYER, _changed[0].aboutId);
            Assert.AreEqual(0, _changed[0].previous);
            Assert.AreEqual(-27, _changed[0].current);
        }

        [Test]
        public void Tick_ZeroDeltaTime_SpreadsNothing()
        {
            WitnessTheft();

            _gossip.Tick(0f);
            _gossip.Tick(0f);

            Assert.IsEmpty(_spread, "A rumor travelled while the game was paused.");
            Assert.AreEqual(2, _gossip.PendingCount);

            _gossip.Tick(HOP_DELAY);

            Assert.AreEqual(1, _spread.Count);
        }

        [Test]
        public void Tick_FartherHopsLandOnLaterTicks()
        {
            _gossip = NewService(smithToElderTrust: 100);

            WitnessTheft();

            _gossip.Tick(HOP_DELAY);
            Assert.AreEqual(-27, _opinions.Get(SMITH, PLAYER));
            Assert.AreEqual(0, _opinions.Get(ELDER, PLAYER), "Hop 2 arrived together with hop 1.");

            _gossip.Tick(HOP_DELAY);
            Assert.AreEqual(-27, _opinions.Get(ELDER, PLAYER),
                "The elder never heard it, so the story stopped one introduction short of the village.");
            Assert.AreEqual(0, _gossip.PendingCount);
        }

        [Test]
        public void Tick_DeliversInHopOrder()
        {
            _gossip = NewService(smithToElderTrust: 100);

            WitnessTheft();

            // One big step releases both hops at once, and they must still arrive near to far.
            _gossip.Tick(HOP_DELAY * 2);

            Assert.AreEqual(2, _spread.Count);
            Assert.AreEqual(1, _spread[0].hop);
            Assert.AreEqual(2, _spread[1].hop);
        }

        [Test]
        public void Restore_AppliesWithoutPublishing()
        {
            _gossip.Restore(new[] { SMITH }, new[] { PLAYER }, new[] { -55 });

            Assert.AreEqual(-55, _opinions.Get(SMITH, PLAYER));
            Assert.IsEmpty(_changed, "Loading a save published changes, so it would have marked itself dirty again.");
            Assert.IsEmpty(_spread);
        }

        [Test]
        public void Restore_NullArraysAreAFreshSave()
        {
            _gossip.Restore(null, null, null);

            Assert.AreEqual(0, _opinions.Count);
            Assert.IsEmpty(_changed);
        }

        [Test]
        public void Restore_MismatchedLengths_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => _gossip.Restore(new[] { SMITH }, new[] { PLAYER, SON }, new[] { -55 }));
        }

        [Test]
        public void Restore_DiscardsRumorsInFlight()
        {
            WitnessTheft();

            _gossip.Restore(new[] { SMITH }, new[] { PLAYER }, new[] { -10 });

            Assert.AreEqual(0, _gossip.PendingCount,
                "A rumor from before the load survived it, and would have moved the same opinion twice.");
        }

        [Test]
        public void Constructor_NullArguments_Throw()
        {
            var propagator = new RumorPropagator(new SocialGraph(new (string, string, int)[0]), 0, 5);

            Assert.Throws<ArgumentNullException>(() => new GossipService(null, propagator, 1f));
            Assert.Throws<ArgumentNullException>(() => new GossipService(_opinions, null, 1f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Constructor_InvalidHopDelay_Throws(float hopDelay)
        {
            var propagator = new RumorPropagator(new SocialGraph(new (string, string, int)[0]), 0, 5);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GossipService(_opinions, propagator, hopDelay));
        }

        #endregion
    }
}
