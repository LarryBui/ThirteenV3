using UnityEngine;
using TienLen.Core.Domain.ValueObjects;
using TienLen.Unity.Presentation.Views;
using TienLen.Unity.Presentation.ScriptableObjects;
using TienLen.Unity.Presentation.Visuals;

namespace TienLen.Unity.Presentation
{
    public class CardFactory : MonoBehaviour
    {
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private CardThemeSO _theme;

        public CardView CreateCard(Card card, Transform parent)
        {
            var cardInstance = Instantiate(_cardPrefab, parent);
            
            // Configure the visual composer with the theme
            var composer = cardInstance.GetComponent<CardVisualComposer>();
            if (composer != null)
            {
                composer.SetTheme(_theme);
            }
            else
            {
                Debug.LogWarning("CardView prefab is missing CardVisualComposer component.");
            }

            cardInstance.Initialize(card);
            return cardInstance;
        }
    }
}
