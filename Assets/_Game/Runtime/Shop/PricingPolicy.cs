using System;

namespace GosipSimulator.Shop
{
    /// <summary>
    /// What a shopkeeper charges somebody, given what it thinks of them. Plain C#, so the whole rule
    /// runs in an EditMode test instead of only being reachable by robbing the blacksmith (R5).
    ///
    /// The opinion moves the price linearly: every point below zero adds <c>percentPerPoint</c> to the
    /// price, every point above zero takes it off, down to a floor. Below a threshold the shopkeeper
    /// stops selling altogether. Two separate consequences on purpose, so that a single rumor makes
    /// things dearer and only a reputation makes the door close.
    /// </summary>
    public class PricingPolicy
    {
        private readonly int _basePrice;
        private readonly int _percentPerPoint;
        private readonly int _minPercent;
        private readonly int _refuseAtOrBelow;

        // ────────────────────────────────
        // CONSTRUCTION
        // ────────────────────────────────
        #region Construction

        /// <param name="basePrice">What a stranger pays, with an opinion of zero. At least one.</param>
        /// <param name="percentPerPoint">How much each point of opinion moves the price. Zero or more.</param>
        /// <param name="minPercent">The best discount anybody can get, as a percentage. 1 to 100.</param>
        /// <param name="refuseAtOrBelow">
        /// The opinion at which the shopkeeper stops selling, inclusive. Set it below the lowest
        /// opinion Gossip allows and the shopkeeper never refuses.
        /// </param>
        public PricingPolicy(int basePrice, int percentPerPoint, int minPercent, int refuseAtOrBelow)
        {
            // A base of zero would make every price zero, and a sale that costs nothing is something
            // Save cannot take payment for. It is rejected here instead of reaching TrySpend.
            if (basePrice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice), basePrice, "The base price must be at least 1.");
            }

            if (percentPerPoint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(percentPerPoint), percentPerPoint,
                    "A negative rate would make a hated customer pay less.");
            }

            // The floor stays above zero for the same reason as the base price, and at or below 100
            // because a floor above the base price would charge a friend more than a stranger.
            if (minPercent < 1 || minPercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(minPercent), minPercent, "The floor must be between 1 and 100.");
            }

            _basePrice       = basePrice;
            _percentPerPoint = percentPerPoint;
            _minPercent      = minPercent;
            _refuseAtOrBelow = refuseAtOrBelow;
        }

        #endregion

        // ────────────────────────────────
        // PUBLIC API
        // ────────────────────────────────
        #region Public API

        public int BasePrice => _basePrice;

        /// <summary>The terms for a customer the shopkeeper holds <paramref name="opinion"/> of.</summary>
        public ShopTerms For(int opinion)
        {
            // In long because nothing here bounds the opinion. Gossip clamps it to its configured
            // range, but this class does not know that range and should not overflow if it changes.
            long percent = 100L - (long)opinion * _percentPerPoint;

            if (percent < _minPercent) percent = _minPercent;
            if (percent > int.MaxValue) percent = int.MaxValue;

            // Rounded up, in the shop's favour. That is what makes one rumor visible at a small base
            // price: 110% of 5 is 5.5, and truncating it back to 5 would hide the markup entirely.
            long price = ((long)_basePrice * percent + 99L) / 100L;

            if (price > int.MaxValue) price = int.MaxValue;

            return new ShopTerms((int)percent, (int)price, opinion <= _refuseAtOrBelow);
        }

        #endregion
    }

    /// <summary>What one customer is offered. Refusing and pricing are independent on purpose.</summary>
    public readonly struct ShopTerms : IEquatable<ShopTerms>
    {
        /// <summary>The price as a percentage of the base. 100 is what a stranger pays.</summary>
        public readonly int Percent;

        /// <summary>Always at least one, so an approved sale can always be paid for.</summary>
        public readonly int Price;

        /// <summary>
        /// True when the shopkeeper will not sell. The price is still computed, because "you would be
        /// charged 16 if you were served at all" is worth being able to show.
        /// </summary>
        public readonly bool Refuses;

        public ShopTerms(int percent, int price, bool refuses)
        {
            Percent = percent;
            Price   = price;
            Refuses = refuses;
        }

        public bool Equals(ShopTerms other) =>
            Percent == other.Percent && Price == other.Price && Refuses == other.Refuses;

        public override bool Equals(object obj) => obj is ShopTerms other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Percent, Price, Refuses);

        public override string ToString() =>
            Refuses ? $"refuses (would be {Price} at {Percent}%)" : $"{Price} at {Percent}%";
    }
}
