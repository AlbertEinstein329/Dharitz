using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles Hybrid Input: Tap to draw (if empty) and Drag-to-place (if holding a die).
/// </summary>
[RequireComponent(typeof(Image))]
public class UIDieInteractor : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    public bool IsSlotEmpty { get; set; } = true;
    public bool isInputLocked { get; set; } = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        // CanvasGroup is required to allow Raycasts to pass through the image while dragging
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        originalAnchoredPosition = rectTransform.anchoredPosition;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Añadimos el candado a la condición (!isInputLocked)
        if (IsSlotEmpty && !eventData.dragging && !isInputLocked)
        {
            GameManager.Instance.DrawDie(); // O el playerName de tu función
            AudioManager.Instance.PlayDrawSound();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsSlotEmpty) return;

        canvasGroup.blocksRaycasts = false; // Let rays pass through to the 2D board
        canvasGroup.alpha = 0.8f; // Make it slightly transparent while dragging
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsSlotEmpty) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsSlotEmpty) return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        if (Camera.main != null)
        {
            // POINT 3: Convert screen position to 2D world position and simulate a click on the cell
            Vector3 worldPoint = Camera.main.ScreenToWorldPoint(eventData.position);
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

            if (hit.collider != null)
            {
                CellComponent targetCell = hit.collider.GetComponent<CellComponent>();
                if (targetCell != null)
                {
                    // Simulate the click on the 2D cell to place the die
                    targetCell.HandleClick();
                }
            }
        }
        else
        {
            Debug.LogError("Main Camera is null! Cannot place die.");
        }

        // Return UI image to its original slot position
        rectTransform.anchoredPosition = originalAnchoredPosition;
    }
}