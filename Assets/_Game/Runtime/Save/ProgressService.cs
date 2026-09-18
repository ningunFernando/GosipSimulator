using System;
using GosipSimulator.Core;

namespace GosipSimulator.Save
{
    /// <summary>
    /// The domain layer: mutates progress and enforces its invariants. Plain C#, no UnityEngine
    /// inheritance, so every rule here runs in an EditMode test in milliseconds (R5).
    /// </summary>
    public class ProgressService
    {
        private readonly SaveData _data;

        public ProgressService(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            _data = data;
        }

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public int Currency => _data.currency;

        public int TotalEarned => _data.totalEarned;

        /// <summary>
        /// Throws on zero or negative amounts: totalEarned is a historical accumulator and letting
        /// AddCurrency(-50) shrink it was legal in the reference project (M6).
        /// </summary>
        public void Earn(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

            _data.currency += amount;
            _data.totalEarned += amount;

            PublishProgress();
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (_data.currency < amount) return false;

            _data.currency -= amount;

            PublishProgress();
            return true;
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private void PublishProgress()
        {
            EventBus.Publish(new OnProgressChanged
            {
                currency = _data.currency,
                totalEarned = _data.totalEarned
            });
        }

        #endregion
    }
}
