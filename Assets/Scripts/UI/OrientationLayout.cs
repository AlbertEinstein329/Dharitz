using UnityEngine;

/// <summary>
/// Disposicion alternativa de un elemento del HUD para pantalla horizontal. Guarda la disposicion original (vertical) al
/// arrancar y aplica la horizontal cuando el canvas es mas ancho que alto; vuelve a la original si se gira de nuevo.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class OrientationLayout : MonoBehaviour
{
    [Header("Disposicion en horizontal")]
    [SerializeField] private Vector2 landscapeAnchorMin;
    [SerializeField] private Vector2 landscapeAnchorMax;
    [SerializeField] private Vector2 landscapePivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 landscapePosition;
    [SerializeField] private float landscapeScale = 1f;

    private RectTransform rect;
    private RectTransform canvasRect;
    private Vector2 portraitAnchorMin, portraitAnchorMax, portraitPivot, portraitPosition;
    private Vector3 portraitScale;
    private int appliedState = -1; // 0 = vertical, 1 = horizontal

    /// <summary>La pantalla actual es horizontal.</summary>
    public static bool IsLandscape(RectTransform canvasRect) => canvasRect != null && canvasRect.rect.width > canvasRect.rect.height;

    private void Awake()
    {
        rect = (RectTransform)transform;
        portraitAnchorMin = rect.anchorMin;
        portraitAnchorMax = rect.anchorMax;
        portraitPivot = rect.pivot;
        portraitPosition = rect.anchoredPosition;
        portraitScale = rect.localScale;
    }

    private void OnEnable()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        appliedState = -1;
        Apply();
    }

    private void LateUpdate() => Apply();

    private void Apply()
    {
        if (canvasRect == null) return;
        int state = IsLandscape(canvasRect) ? 1 : 0;
        if (state == appliedState) return;
        appliedState = state;

        if (state == 1)
        {
            rect.anchorMin = landscapeAnchorMin;
            rect.anchorMax = landscapeAnchorMax;
            rect.pivot = landscapePivot;
            rect.anchoredPosition = landscapePosition;
            rect.localScale = new Vector3(landscapeScale, landscapeScale, 1f);
        }
        else
        {
            rect.anchorMin = portraitAnchorMin;
            rect.anchorMax = portraitAnchorMax;
            rect.pivot = portraitPivot;
            rect.anchoredPosition = portraitPosition;
            rect.localScale = portraitScale;
        }

        // La posicion base ha cambiado: el margen de area segura se recalcula sobre la nueva disposicion
        SafeAreaOffset safe = GetComponent<SafeAreaOffset>();
        if (safe != null) safe.Recapture();
    }
}
