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
        //Intentionally left empty to stop the translucent UI die bug!
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Intentionally left empty
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Intentionally left empty
    }
}