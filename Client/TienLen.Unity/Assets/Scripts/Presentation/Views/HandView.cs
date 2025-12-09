using System.Collections.Generic;
using UnityEngine;
using TienLen.Unity.Domain.ValueObjects;

using Cysharp.Threading.Tasks;

namespace TienLen.Unity.Presentation.Views
{
    public class HandView : MonoBehaviour
    {
        public CardView CardPrefab; // Assign in Inspector
        public Transform CardContainer; // Assign in Inspector (Horizontal Layout Group)

        private List<CardView> _activeCards = new List<CardView>();

        public List<Card> GetSelectedCards()
        {
            var selected = new List<Card>();
            foreach (var cardView in _activeCards)
            {
                if (cardView.IsSelected)
                {
                    selected.Add(cardView.CardData);
                }
            }
            return selected;
        }

        public void RenderHand(IEnumerable<Card> cards)
        {
            // RenderHandAnimated(cards).Forget();

            // Clear existing immediately
            ClearHand();
            // Spawn new
            foreach (var card in cards)
            {
                var go = Instantiate(CardPrefab, CardContainer);
                go.Initialize(card);
                _activeCards.Add(go);
            }
        }

        private async UniTaskVoid RenderHandAnimated(IEnumerable<Card> cards)
        {
            Debug.Log($"[HandView] Rendering hand with {GetCardCount(cards)} cards.");
            
            // Clear existing immediately
            ClearHand();

            // Spawn new with delay
            foreach (var card in cards)
            {
                var go = Instantiate(CardPrefab, CardContainer);
                go.Initialize(card);
                _activeCards.Add(go);
                
                await UniTask.Delay(300); // 300ms delay per card
            }
            Debug.Log("[HandView] Finished rendering hand.");
        }

        private int GetCardCount(IEnumerable<Card> cards)
        {
            return System.Linq.Enumerable.Count(cards);
        }
        
        private void ClearHand()
        {
            foreach (var cardView in _activeCards)
            {
                if (cardView != null) Destroy(cardView.gameObject);
            }
            _activeCards.Clear();
        }
        
        public void RemoveCards(List<Card> cardsToRemove)
        {
            // Simple re-render or targeted removal. 
            // For prototype, we'll assume the Presenter calls RenderHand with the fresh state.
        }
    }
}
