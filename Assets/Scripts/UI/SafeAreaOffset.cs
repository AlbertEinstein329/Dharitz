using UnityEngine;

/// <summary>
/// Desplaza un elemento de la interfaz anclado a un borde de la pantalla para que no quede bajo el notch ni la barra
/// de gestos. Detecta el borde por las anclas (arriba, abajo, izquierda, derecha). Se aplica a los grupos del HUD
/// que tocan el borde, no a los fondos de pantalla completa.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaOffset : MonoBehaviour
{
    private RectTransform rect;
    private RectTransform canvasRect;
    private Vector2 basePosition;
    private bool captured;
    private int appliedVersion = -1;

    private void OnEnable()
    {
        rect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        appliedVersion = -1;
        Apply();
    }

    private void OnDisable()
    {
        if (captured && rect != null) rect.anchoredPosition = basePosition;
        appliedVersion = -1;
    }

    private void LateUpdate() => Apply();

    /// <summary>La posicion base cambio (p. ej. al girar la pantalla): se vuelve a capturar y a aplicar el margen.</summary>
    public void Recapture()
    {
        captured = false;
        appliedVersion = -1;
        if (isActiveAndEnabled) Apply();
    }

    private void Apply()
    {
        if (canvasRect == null) return;
        SafeInsets.Refresh(canvasRect);
        if (appliedVersion == SafeInsets.Version) return;
        appliedVersion = SafeInsets.Version;

        if (!captured)
        {
            basePosition = rect.anchoredPosition;
            captured = true;
        }

        bool top = rect.anchorMin.y >= 0.999f;      // anclado arriba
        bool bottom = rect.anchorMax.y <= 0.001f;   // anclado abajo
        bool left = rect.anchorMax.x <= 0.001f;     // anclado a la izquierda
        bool right = rect.anchorMin.x >= 0.999f;    // anclado a la derecha

        float dx = (left ? SafeInsets.Left : 0f) - (right ? SafeInsets.Right : 0f);
        float dy = (bottom ? SafeInsets.Bottom : 0f) - (top ? SafeInsets.Top : 0f);
        rect.anchoredPosition = basePosition + new Vector2(dx, dy);
    }
}
