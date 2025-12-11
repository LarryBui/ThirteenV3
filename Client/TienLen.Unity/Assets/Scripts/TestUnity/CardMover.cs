using UnityEngine;
using DG.Tweening; // Required Namespace

public class CardMover : MonoBehaviour
{
    // Configuration usually belongs in a ScriptableObject or const file, 
    // but exposed here for Inspector tweaking.
    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private Ease movementEase = Ease.OutQuad; // "OutQuad" feels natural for cards (fast start, slow stop)

    private RectTransform _rectTransform;

    private void Awake()
    {
        // Cache your transform. 
        // In UI context, getting RectTransform is slightly safer for casting.
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Moves the card to a target position in World Space.
    /// </summary>
    /// <param name="targetPosition">The world position to move to.</param>
    /// <param name="onComplete">Optional callback when movement finishes.</param>
    public void MoveTo(Vector3 targetPosition, System.Action onComplete = null)
    {
        // 1. SAFETY: Kill any existing tweens on this object.
        // If the player clicks the card twice rapidly, this prevents the tweens from fighting.
        transform.DOKill();

        // 2. MOVEMENT:
        // DOMove takes (Destination, Duration).
        transform.DOMove(targetPosition, moveDuration)
            .SetEase(movementEase) // Apply the specific ease curve
            .SetLink(gameObject)   // CRITICAL: If this card is Destroyed mid-move, kill the tween automatically.
            .OnComplete(() => 
            {
                // 3. CALLBACK:
                // Invoke the action if it exists (e.g., tell the TurnManager the card arrived).
                onComplete?.Invoke(); 
            });
    }

    /// <summary>
    /// Example for moving to a specific UI container (like a Hand or Discard Pile)
    /// </summary>
    public void MoveToContainer(Transform containerParent)
    {
        transform.DOKill();
        
        // Changing parent handles the hierarchy, but the position might snap.
        // Usually, we parent first, then tween local position to 0,0,0.
        transform.SetParent(containerParent);
        
        transform.DOLocalMove(Vector3.zero, moveDuration)
            .SetEase(movementEase)
            .SetLink(gameObject);
    }
}