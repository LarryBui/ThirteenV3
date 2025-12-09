using UnityEngine;
using UnityEngine.EventSystems;
using System;
using TienLen.Unity.Domain.ValueObjects;
using TienLen.Unity.Presentation.Visuals; // New Namespace

namespace TienLen.Unity.Presentation.Views
{
    [RequireComponent(typeof(CardVisualComposer))]
    public class CardView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Components")]
        [SerializeField] private CardVisualComposer _visualComposer;
        public UnityEngine.UI.Image SelectionHighlight; // Keep this for selection logic

        public Card CardData { get; private set; }
        public bool IsSelected { get; private set; }

        public event Action<CardView> OnCardClicked;

        private void Awake()
        {
            if (_visualComposer == null) _visualComposer = GetComponent<CardVisualComposer>();
        }

        public void Initialize(Card card)
        {
            CardData = card;
            name = card.ToString();
            IsSelected = false;

            // Delegate rendering to the composer
            _visualComposer.Render(card);
            
            UpdateVisuals();
        }

        public void ShowBack()
        {
            _visualComposer.ShowBack();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            IsSelected = !IsSelected;
            UpdateVisuals();
            OnCardClicked?.Invoke(this);
        }

        private void UpdateVisuals()
        {
            if (SelectionHighlight != null)
            {
                SelectionHighlight.enabled = IsSelected;
            }
            
            // Simple animation: Pop up if selected
            // Using localPosition.y = 20 as a simple visual cue
            transform.localPosition = new Vector3(transform.localPosition.x, IsSelected ? 20 : 0, transform.localPosition.z);
        }
    }
}