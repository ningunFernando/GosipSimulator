using System;
using System.Collections.Generic;
using NUnit.Framework;
using GosipSimulator.Core;
using GosipSimulator.Save;

namespace GosipSimulator.Tests
{
    /// <summary>
    /// Covers M6: in the reference project AddCurrency(-50) was legal and decremented totalEarned,
    /// a lifetime accumulator that can only ever grow. The invariants live here rather than in the
    /// MonoBehaviour, which is what makes them testable at all (R5).
    /// </summary>
    public class ProgressServiceTests
    {
        private readonly List<OnProgressChanged> _published = new List<OnProgressChanged>();

        private Action<OnProgressChanged> _sink;
        private SaveData        _data;
        private ProgressService _progress;

        // ────────────────────────────────
        // SETUP
        // ────────────────────────────────
        #region Setup

        [SetUp]
        public void SetUp()
        {
            EventBus.ClearAllSubscriptions();
            _published.Clear();

            // Standing in for the SaveSystem, which is the real subscriber. Without it every
            // mutation would publish into an empty bus and warn about it.
            _sink = e => _published.Add(e);
            EventBus.Subscribe(_sink);

            _data     = new SaveData();
            _progress = new ProgressService(_data);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe(_sink);
            EventBus.ClearAllSubscriptions();
        }

        #endregion

        // ────────────────────────────────
        // TESTS
        // ────────────────────────────────
        #region Tests

        [Test]
        public void Earn_NegativeAmount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _progress.Earn(-50));

            Assert.AreEqual(0, _progress.Currency, "A rejected Earn still moved the balance.");
            Assert.AreEqual(0, _progress.TotalEarned);
            Assert.IsEmpty(_published, "A rejected Earn still published a progress change.");
        }

        [Test]
        public void Earn_Zero_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _progress.Earn(0));
        }

        [Test]
        public void Earn_AddsToCurrencyAndTotalEarned()
        {
            _progress.Earn(100);

            Assert.AreEqual(100, _progress.Currency);
            Assert.AreEqual(100, _progress.TotalEarned);
        }

        [Test]
        public void TrySpend_WithEnoughCurrency_LeavesTotalEarnedIntact()
        {
            _progress.Earn(100);

            Assert.IsTrue(_progress.TrySpend(30));

            Assert.AreEqual(70, _progress.Currency);

            // The heart of M6: spending moves the balance and never the lifetime accumulator.
            Assert.AreEqual(100, _progress.TotalEarned, "Spending decremented the lifetime accumulator.");
        }

        [Test]
        public void TrySpend_MoreThanBalance_ReturnsFalseAndChangesNothing()
        {
            _progress.Earn(20);
            _published.Clear();

            Assert.IsFalse(_progress.TrySpend(50));

            Assert.AreEqual(20, _progress.Currency);
            Assert.AreEqual(20, _progress.TotalEarned);
            Assert.IsEmpty(_published, "A refused purchase still published a progress change.");
        }

        [Test]
        public void TrySpend_NegativeAmount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _progress.TrySpend(-10));
        }

        [Test]
        public void Earn_PublishesProgressWithBothValues()
        {
            _progress.Earn(75);

            Assert.AreEqual(1, _published.Count, "The mutation never reached the bus.");
            Assert.AreEqual(75, _published[0].currency);
            Assert.AreEqual(75, _published[0].totalEarned);
        }

        [Test]
        public void Constructor_NullData_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ProgressService(null));
        }

        #endregion
    }
}
