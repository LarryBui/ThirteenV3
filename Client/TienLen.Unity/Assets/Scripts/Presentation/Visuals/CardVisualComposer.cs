using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TienLen.Core.Domain.ValueObjects;
using TienLen.Unity.Presentation.ScriptableObjects;
using TienLen.Unity.Presentation.Visuals.Elements;

namespace TienLen.Unity.Presentation.Visuals
{
    public class CardVisualComposer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _backImage; // The back of the card
        
        [Header("Configuration")]
        [SerializeField] private CardThemeSO _theme;

        private ICardVisualElement[] _elements;

        private void Awake()
        {
            // Auto-discover all atomic elements in children
            _elements = GetComponentsInChildren<ICardVisualElement>(true);
        }

        public void SetTheme(CardThemeSO theme)
        {
            _theme = theme;
        }

        public void Render(Card card)
        {
            if (_theme == null)
            {
                Debug.LogError("No CardTheme assigned to Composer!");
                return;
            }

            // 1. Setup Base Visuals
            if (_backgroundImage != null) _backgroundImage.sprite = _theme.CardFaceBackground;
            
            // 2. Propagate to all child elements (Rank, Suit, etc.)
            foreach (var element in _elements)
            {
                element.Render(card, _theme);
                element.SetVisible(true); // Ensure face elements are showing
            }

            // 3. Hide Back
            if (_backImage != null) _backImage.gameObject.SetActive(false);
        }

        public void ShowBack()
        {
             if (_backImage != null && _theme != null)
             {
                 _backImage.sprite = _theme.CardBack;
                 _backImage.gameObject.SetActive(true);
             }
        }
    }
}
