using UnityEngine;

/// <summary>
/// Reduce un panel de tamano fijo (menu online, ajustes...) cuando no cabe en el canvas, por ejemplo al girar el movil
/// a horizontal. Nunca lo agranda. Se anade solo a los paneles que crea <see cref="OnlineUiKit"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class PanelFit : MonoBehaviour
{
    [Tooltip("Fraccion del canvas que el panel puede ocupar como maximo.")]
    [SerializeField] private float maxFill = 0.94f;

    private RectTransform rect;
    private RectTransform canvasRect;
    private Vector2 lastCanvasSize;

    private void OnEnable()
    {
        rect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        lastCanvasSize = Vector2.zero;
        Fit();
    }

    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (canvasRect == null) return;
        Vector2 canvasSize = canvasRect.rect.size;
        if (canvasSize == lastCanvasSize) return;
        lastCanvasSize = canvasSize;

        Vector2 size = rect.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;

        float scale = Mathf.Min(1f, canvasSize.x * maxFill / size.x, canvasSize.y * maxFill / size.y);
        rect.localScale = new Vector3(scale, scale, 1f);
    }
}
