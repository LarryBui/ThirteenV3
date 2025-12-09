using UnityEngine;
using System.Collections.Generic;
using TienLen.Core.Domain.Enums;
using TMPro;

namespace TienLen.Unity.Presentation.ScriptableObjects
{
    [CreateAssetMenu(fileName = "NewCardTheme", menuName = "TienLen/Card Theme")]
    public class CardThemeSO : ScriptableObject
    {
        [Header("Assets")]
        public Sprite CardBack;
        public Sprite CardFaceBackground;
        
        [Header("Typography")]
        public TMP_FontAsset RankFont;
        
        [Header("Colors")]
        public Color RedSuitColor = Color.red;
        public Color BlackSuitColor = Color.black;

        [Header("Suit Icons")]
        [SerializeField] private Sprite _spadeIcon;
        [SerializeField] private Sprite _clubIcon;
        [SerializeField] private Sprite _diamondIcon;
        [SerializeField] private Sprite _heartIcon;

        [Header("Court Cards (J, Q, K) - Optional")]
        [SerializeField] private List<CourtCardMapping> _courtCards;

        [System.Serializable]
        public struct CourtCardMapping
        {
            public Rank Rank;
            public Sprite Portrait;
        }

        public Sprite GetSuitIcon(Suit suit)
        {
            return suit switch
            {
                Suit.Spades => _spadeIcon,
                Suit.Clubs => _clubIcon,
                Suit.Diamonds => _diamondIcon,
                Suit.Hearts => _heartIcon,
                _ => null
            };
        }

        public Color GetSuitColor(Suit suit)
        {
            return (suit == Suit.Diamonds || suit == Suit.Hearts) ? RedSuitColor : BlackSuitColor;
        }

        public string GetRankLabel(Rank rank)
        {
            return rank switch
            {
                Rank.Three => "3",
                Rank.Four => "4",
                Rank.Five => "5",
                Rank.Six => "6",
                Rank.Seven => "7",
                Rank.Eight => "8",
                Rank.Nine => "9",
                Rank.Ten => "10",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                Rank.Ace => "A",
                Rank.Two => "2",
                _ => "?"
            };
        }
    }
}
