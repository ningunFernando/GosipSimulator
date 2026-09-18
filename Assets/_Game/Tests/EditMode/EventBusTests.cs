using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GosipSimulator.Core;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Covers A1: in the reference project the bus had three Publish call sites and zero
    /// Subscribe, so nothing here had ever run. These tests are what makes the bus a spine
    /// instead of decoration.
    /// </summary>
    public class EventBusTests
    {
        private static readonly Regex NoSubscribers = new Regex(@"^\[EventBus\].*no subscribers");

        /// <summary>
        /// Local to the tests on purpose: publishing a real game event here would collide with
        /// whatever else subscribes to it and would test two things at once.
        /// </summary>
        private struct TestEvent
        {
            public int value;
        }

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        /// <summary>
        /// The bus is static, so state survives between tests. Clearing on both ends means a
        /// failing test cannot cascade into the next one.
        /// </summary>
        [SetUp]
        public void SetUp() => EventBus.ClearAllSubscriptions();

        [TearDown]
        public void TearDown() => EventBus.ClearAllSubscriptions();

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Publish_WithSubscriber_InvokesCallback()
        {
            int received = 0;
            Action<TestEvent> callback = e => received = e.value;

            EventBus.Subscribe(callback);
            EventBus.Publish(new TestEvent { value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_WithNoSubscriber_DoesNotThrow()
        {
            // The warning is part of the contract, not noise: a silent bus is indistinguishable
            // from a broken one, which is exactly why A1 went unnoticed.
            LogAssert.Expect(LogType.Warning, NoSubscribers);

            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent()));
        }

        [Test]
        public void Unsubscribe_LastSubscriber_RemovesEventType()
        {
            Action<TestEvent> callback = _ => { };

            EventBus.Subscribe(callback);
            EventBus.Unsubscribe(callback);

            // The dictionary is private. The observable proof that the key is gone is that
            // Publish reports "no subscribers" rather than invoking an empty delegate.
            LogAssert.Expect(LogType.Warning, NoSubscribers);
            EventBus.Publish(new TestEvent());
        }

        [Test]
        public void Unsubscribe_OneOfTwoSubscribers_KeepsTheOther()
        {
            int leaving = 0;
            int staying = 0;

            Action<TestEvent> first  = _ => leaving++;
            Action<TestEvent> second = _ => staying++;

            EventBus.Subscribe(first);
            EventBus.Subscribe(second);
            EventBus.Unsubscribe(first);

            EventBus.Publish(new TestEvent());

            Assert.AreEqual(0, leaving, "The removed subscriber was still invoked.");
            Assert.AreEqual(1, staying, "Removing one subscriber removed the other as well.");
        }

        [Test]
        public void ClearAllSubscriptions_RemovesEverySubscriber()
        {
            int received = 0;
            EventBus.Subscribe<TestEvent>(_ => received++);

            EventBus.ClearAllSubscriptions();

            LogAssert.Expect(LogType.Warning, NoSubscribers);
            EventBus.Publish(new TestEvent());

            Assert.AreEqual(0, received);
        }

        [Test]
        public void Subscribe_NullCallback_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => EventBus.Subscribe<TestEvent>(null));
        }

        #endregion
    }
}
