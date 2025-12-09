using TienLen.Core.Domain.ValueObjects;
using TienLen.Unity.Presentation.ScriptableObjects;

namespace TienLen.Unity.Presentation.Visuals.Elements
{
    /// <summary>
    /// Contract for any atomic part of a card (Rank Text, Suit Icon, Border, etc.)
    /// </summary>
    public interface ICardVisualElement
    {
        void Render(Card cardData, CardThemeSO theme);
        void SetVisible(bool isVisible);
    }
}
