using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

namespace TienLen.Unity.Presentation.Views
{
    public class CardDealer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GameObject _cardPrefab;
        [SerializeField] private RectTransform _deckPosition;
        [SerializeField] private RectTransform _cardContainer;
        [Tooltip("Distance from the screen border to stop the card")]
        [SerializeField] private float _borderPadding = 200f;
        [SerializeField] private float _dealDuration = 0.25f;
        [SerializeField] private float _dealInterval = 0.2f;

        [Header("Events")]
        public UnityEvent OnDealComplete;

        [Header("Debug")]
        [SerializeField] private bool _simulateDeal = false;

        private RectTransform _canvasRect;

        private void Awake()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _canvasRect = canvas.GetComponent<RectTransform>();
            }
            else
            {
                Debug.LogError("CardDealer: No Canvas found in parents!");
            }
        }

        private void Update()
        {
            if (_simulateDeal)
            {
                _simulateDeal = false;
                DealCards();
            }
        }

        public void DealCards()
        {
            if (_cardPrefab == null || _canvasRect == null) return;
            
            StartCoroutine(DealRoutine());
        }

        private IEnumerator DealRoutine()
        {
            // Order: North, East, South, West
            Vector2[] targets = new Vector2[]
            {
                GetNorthPosition(),
                GetEastPosition(),
                GetSouthPosition(),
                GetWestPosition()
            };

            for (int i = 0; i < 13; i++)
            {
                foreach (var targetPos in targets)
                {
                    SpawnAndAnimateTo(targetPos);
                    yield return new WaitForSeconds(_dealInterval);
                }
            }

            // Wait for the last card's animation to finish
            yield return new WaitForSeconds(_dealDuration);
            OnDealComplete?.Invoke();
        }

        private void SpawnAndAnimateTo(Vector2 targetLocalPosition)
        {
            // Determine spawn parent
            Transform parent = _cardContainer != null ? _cardContainer : transform;
            Vector3 spawnWorldPos = _deckPosition != null ? _deckPosition.position : transform.position;

            GameObject cardObj = Instantiate(_cardPrefab, parent);
            cardObj.transform.position = spawnWorldPos;

            // Ensure we are dealing with a RectTransform
            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                // If the card container is the same scope as the calculated targets (Canvas scope),
                // we can use DOAnchorPos. 
                // However, targetLocalPosition is calculated based on the CANVAS size/center.
                // If 'parent' is NOT the canvas (e.g. a smaller panel), this might be offset.
                // For safety in this specific task (dealing to screen borders), 
                // we assume the card container is a full-screen panel or centered equivalent.
                
                cardRect.anchoredPosition = Vector2.zero; // Reset to center relative to parent if parent is centered

                cardRect.DOAnchorPos(targetLocalPosition, _dealDuration)
                    .SetEase(Ease.OutQuad);

                // Optional: Rotate the card to face the player? 
                // The prompt didn't ask, but usually:
                // North: 0/180, East: 90, South: 0, West: -90
            }
            
            // If the prefab has a CardView, we might want to show the back
            var view = cardObj.GetComponent<CardView>();
            if (view != null)
            {
                view.ShowBack();
            }
        }

        // Calculations assume (0,0) is the center of the Canvas
        private Vector2 GetNorthPosition()
        {
            float limit = _canvasRect.rect.height / 2f;
            return new Vector2(0, limit - _borderPadding);
        }

        private Vector2 GetSouthPosition()
        {
            float limit = _canvasRect.rect.height / 2f;
            return new Vector2(0, -(limit - _borderPadding));
        }

        private Vector2 GetEastPosition()
        {
            float limit = _canvasRect.rect.width / 2f;
            return new Vector2(limit - _borderPadding, 0);
        }

        private Vector2 GetWestPosition()
        {
            float limit = _canvasRect.rect.width / 2f;
            return new Vector2(-(limit - _borderPadding), 0);
        }
    }
}
