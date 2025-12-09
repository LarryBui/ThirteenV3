using System;
using TienLen.Unity.Domain.Enums;

namespace TienLen.Unity.Domain.ValueObjects
{
    [Serializable]
    public class Card : IComparable<Card>
    {
        public Rank Rank { get; private set; }
        public Suit Suit { get; private set; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public int CompareTo(Card other)
        {
            if (other == null) return 1;

            if (Rank != other.Rank)
                return Rank.CompareTo(other.Rank);

            return Suit.CompareTo(other.Suit);
        }

        public override string ToString()
        {
            return $"{Rank} of {Suit}";
        }

        // Helper to check equality if needed
        public override bool Equals(object obj)
        {
            if (obj is Card other)
            {
                return Rank == other.Rank && Suit == other.Suit;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Rank, Suit);
        }
    }
}
