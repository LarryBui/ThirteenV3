using System.Collections.Generic;
using UnityEngine;
using TienLen.Unity.Domain.ValueObjects;
using Cysharp.Threading.Tasks;
#if DOTWEEN
using DG.Tweening;
#endif

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
        public async UniTask AnimateAndAddPlayedCards(List<Card> cards, Vector3 fallbackStartWorldPosition, Dictionary<Card, Vector3> cardStartPositions = null)
        {
            if (cards == null || cards.Count == 0)
            {
                ClearBoard();
                return;
            }

            ClearBoard();

            foreach (var cardData in cards)
            {
                var startPosition = fallbackStartWorldPosition;
                if (cardStartPositions != null && cardStartPositions.TryGetValue(cardData, out var overridePosition))
                {
                    startPosition = overridePosition;
                }

                CardView cardInstance = Instantiate(CardPrefab, transform);
                cardInstance.Initialize(cardData);
                
                cardInstance.transform.position = startPosition;
                
                await AnimateCardToBoard(cardInstance.transform);
                
                cardInstance.transform.SetParent(PlayedCardsContainer, worldPositionStays: true);
                
                _activeBoardCards.Add(cardInstance);
                // Optional: add a slight delay between cards if playing multiple
                // await UniTask.Delay(50); 
            }
        }

        private async UniTask AnimateCardToBoard(Transform cardTransform)
        {
#if DOTWEEN
            var tween = cardTransform.DOMove(PlayedCardsContainer.position, AnimationDuration).SetEase(Ease.OutQuad);
            await tween.AsyncWaitForCompletion();
#else
            await cardTransform.MoveToAsync(PlayedCardsContainer.position, AnimationDuration, Easing.OutQuad);
#endif
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
