using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TienLen.Core.Domain.ValueObjects;
using TienLen.Core.Domain.Enums;

namespace TienLen.Unity.Presentation.ScriptableObjects
{
    [CreateAssetMenu(fileName = "CardSpriteRegistry", menuName = "TienLen/Card Sprite Registry")]
    public class CardSpriteRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct CardSpriteMapping
        {
            public Rank Rank;
            public Suit Suit;
            public Sprite Sprite;
        }

        [SerializeField]
        private List<CardSpriteMapping> _cardSprites;
        
        [SerializeField]
        private Sprite _cardBack;

        public Sprite GetSprite(Card card)
        {
            var mapping = _cardSprites.FirstOrDefault(x => x.Rank == card.Rank && x.Suit == card.Suit);
            return mapping.Sprite;
        }

        public Sprite GetBack() => _cardBack;
    }
}
