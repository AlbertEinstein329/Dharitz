using UnityEngine;

/// <summary>Ajusta un contenedor a pantalla completa para que ocupe solo el area segura (para UI creada por codigo).</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rect;
    private RectTransform canvasRect;
    private int appliedVersion = -1;

    private void OnEnable()
    {
        rect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        appliedVersion = -1;
        Fit();
    }

    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (canvasRect == null) return;
        SafeInsets.Refresh(canvasRect);
        if (appliedVersion == SafeInsets.Version) return;
        appliedVersion = SafeInsets.Version;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(SafeInsets.Left, SafeInsets.Bottom);
        rect.offsetMax = new Vector2(-SafeInsets.Right, -SafeInsets.Top);
    }
}
