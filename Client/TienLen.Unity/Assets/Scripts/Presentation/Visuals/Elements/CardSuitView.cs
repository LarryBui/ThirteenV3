using UnityEngine;
using UnityEngine.UI;
using TienLen.Core.Domain.ValueObjects;
using TienLen.Unity.Presentation.ScriptableObjects;

namespace TienLen.Unity.Presentation.Visuals.Elements
{
    public class CardSuitView : MonoBehaviour, ICardVisualElement
    {
        [SerializeField] private Image _suitImage;

        public void Render(Card cardData, CardThemeSO theme)
        {
            if (_suitImage == null) return;

            _suitImage.sprite = theme.GetSuitIcon(cardData.Suit);
            _suitImage.color = theme.GetSuitColor(cardData.Suit); // Optional: Tint the icon
        }
        
        public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);
    }
}
