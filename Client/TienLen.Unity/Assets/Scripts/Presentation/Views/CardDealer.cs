using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using System;
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
        [SerializeField] private float _dealDuration = 0.1f;
        [SerializeField] private float _dealInterval = 0.005f;

        [Header("Events")]
        public UnityEvent OnDealComplete;
        public event Action OnSouthCardArrived;

        [Header("Debug")]
        [SerializeField] private bool _simulateDeal = false;

        private RectTransform _canvasRect;
        private Sequence _dealingSequence;

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

        private void OnDestroy()
        {
            // Kill sequence if object is destroyed to prevent errors
            if (_dealingSequence != null)
            {
                _dealingSequence.Kill();
            }
        }

        public void DealCards()
        {
            if (_cardPrefab == null || _canvasRect == null) return;
            
            // Kill existing sequence if running
            if (_dealingSequence != null && _dealingSequence.IsActive())
            {
                _dealingSequence.Kill();
            }

            StartDealSequence();
        }

        private void StartDealSequence()
        {
            _dealingSequence = DOTween.Sequence();

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
                for (int j = 0; j < targets.Length; j++)
                {
                    // Capture variables for the callback closure
                    Vector2 targetPos = targets[j];
                    bool isSouth = (j == 2);

                    _dealingSequence.AppendCallback(() => SpawnAndAnimateTo(targetPos, isSouth));
                    _dealingSequence.AppendInterval(_dealInterval);
                }
            }

            // Wait for the last card's flight to finish
            _dealingSequence.AppendInterval(_dealDuration);
            _dealingSequence.AppendCallback(() => OnDealComplete?.Invoke());
        }

        private void SpawnAndAnimateTo(Vector2 targetLocalPosition, bool isSouth = false)
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
                cardRect.anchoredPosition = Vector2.zero; // Reset to center relative to parent

                cardRect.DOAnchorPos(targetLocalPosition, _dealDuration)
                    .SetEase(Ease.OutCubic)
                    .OnComplete(() => 
                    {
                        cardObj.SetActive(false);
                        if (isSouth) OnSouthCardArrived?.Invoke();
                    });
                
                // Add rotation animation
                cardRect.DORotate(new Vector3(0, 0, 360), _dealDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear); // Reverted to Linear

            }
            
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