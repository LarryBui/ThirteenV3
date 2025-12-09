using UnityEngine;
using TMPro;
using TienLen.Core.Domain.ValueObjects;
using TienLen.Unity.Presentation.ScriptableObjects;

namespace TienLen.Unity.Presentation.Visuals.Elements
{
    public class CardRankView : MonoBehaviour, ICardVisualElement
    {
        [SerializeField] private TMP_Text _rankText;

        public void Render(Card cardData, CardThemeSO theme)
        {
            if (_rankText == null) return;

            _rankText.text = theme.GetRankLabel(cardData.Rank);
            _rankText.font = theme.RankFont;
            _rankText.color = theme.GetSuitColor(cardData.Suit);
        }

        public void SetVisible(bool isVisible) => gameObject.SetActive(isVisible);
    }
}
