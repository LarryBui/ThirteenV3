using System.Collections.Generic;
using System.Linq;
using TienLen.Unity.Domain.ValueObjects;

namespace TienLen.Unity.Domain.Aggregates
{
    public class Hand
    {
        private List<Card> _cards;

        public IReadOnlyList<Card> Cards => _cards;

        public Hand()
        {
            _cards = new List<Card>();
        }

        public void AddCard(Card card)
        {
            _cards.Add(card);
        }

        public void Sort()
        {
            _cards.Sort();
        }

        public void RemoveCard(Card card)
        {
            _cards.Remove(card);
        }

        public void Clear()
        {
            _cards.Clear();
        }
    }
}
