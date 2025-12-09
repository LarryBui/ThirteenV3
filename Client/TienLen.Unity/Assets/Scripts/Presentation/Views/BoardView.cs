using System.Collections.Generic;
using UnityEngine;
using TienLen.Unity.Domain.ValueObjects;
using Cysharp.Threading.Tasks;

namespace TienLen.Unity.Presentation.Views
{
    public class BoardView : MonoBehaviour
    {
        public Transform PlayedCardsContainer; // Drag the GameObject where played cards will gather
        public CardView CardPrefab; // The card prefab to instantiate for played cards
        public float AnimationDuration = 0.5f;

        private List<CardView> _activeBoardCards = new List<CardView>();

        /// <summary>
        /// Animates cards from a starting position to the board and adds them.
        /// </summary>
        /// <param name="cards">The list of cards to display.</param>
        /// <param name="startWorldPosition">The world position where the animation starts (e.g., player's hand position).</param>
        public async UniTask AnimateAndAddPlayedCards(List<Card> cards, Vector3 startWorldPosition)
        {
            // Clear previous cards on the board, or manage them. For now, clear.
            ClearBoard();

            foreach (var cardData in cards)
            {
                // Instantiate as child of BoardView (this transform) to ensure it's on the Canvas
                // but not yet in the LayoutGroup of PlayedCardsContainer
                CardView cardInstance = Instantiate(CardPrefab, transform);
                cardInstance.Initialize(cardData);
                
                // Set starting position
                cardInstance.transform.position = startWorldPosition;
                
                // Animate it to the PlayedCardsContainer position
                await cardInstance.transform.MoveToAsync(PlayedCardsContainer.position, AnimationDuration, Easing.OutQuad);
                
                // Parent it to the container after animation (LayoutGroup takes over)
                cardInstance.transform.SetParent(PlayedCardsContainer);
                
                _activeBoardCards.Add(cardInstance);
                // Optional: add a slight delay between cards if playing multiple
                // await UniTask.Delay(50); 
            }
        }

        public void ClearBoard()
        {
            foreach (var cardView in _activeBoardCards)
            {
                if (cardView != null) Destroy(cardView.gameObject);
            }
            _activeBoardCards.Clear();
        }
    }
}
