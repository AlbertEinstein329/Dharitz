using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas unico de la escena. Todo lo que se crea por codigo (menu online, HUD online, ajustes, avisos) cuelga de este
/// canvas en lugar de crear el suyo: un solo escalado, un solo raycaster y un orden de dibujo predecible.
/// Capas (de atras hacia delante): los paneles de la propia escena, <see cref="Overlays"/> (paneles modales y ajustes)
/// y <see cref="Toasts"/> (avisos, siempre encima). Va en el objeto del Canvas principal de cada escena.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class UiRoot : MonoBehaviour
{
    public static UiRoot Instance { get; private set; }

    private RectTransform overlays;
    private RectTransform toasts;
    private CanvasScaler scaler;
    private Vector2Int lastScreen;

    /// <summary>Canvas raiz (ya escalado con CanvasScaler).</summary>
    public RectTransform Root => (RectTransform)transform;

    /// <summary>Capa para paneles modales (ajustes, online, game over online). Siempre por encima de los paneles de escena.</summary>
    public RectTransform Overlays
    {
        get
        {
            EnsureLayers();
            return overlays;
        }
    }

    /// <summary>Capa superior para avisos y textos flotantes.</summary>
    public RectTransform Toasts
    {
        get
        {
            EnsureLayers();
            return toasts;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[UiRoot] Ya existe un UiRoot ({Instance.name}); {name} se ignora.");
            return;
        }

        Instance = this;
        scaler = GetComponent<CanvasScaler>();
        EnsureLayers();
        FitScale();
    }

    private void LateUpdate()
    {
        SafeInsets.Refresh(Root);
        Vector2Int pixels = CanvasPixels();
        if (pixels != lastScreen) FitScale();
    }

    private Vector2Int CanvasPixels()
    {
        Rect pixels = GetComponent<Canvas>().pixelRect;
        return new Vector2Int(Mathf.RoundToInt(pixels.width), Mathf.RoundToInt(pixels.height));
    }

    /// <summary>
    /// Escalado segun la pantalla: mas estrecha que la de referencia (movil alto) se ajusta al ancho para no recortar
    /// por los lados; igual o mas ancha (16:9, tabletas) se ajusta al alto para que los elementos no crezcan de mas.
    /// </summary>
    private void FitScale()
    {
        lastScreen = CanvasPixels();
        if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || lastScreen.y <= 0) return;

        float screenAspect = (float)lastScreen.x / lastScreen.y;
        float referenceAspect = scaler.referenceResolution.x / scaler.referenceResolution.y;
        scaler.matchWidthOrHeight = screenAspect >= referenceAspect ? 1f : 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Crea una capa a pantalla completa (sin imagen, no intercepta clics por si sola).</summary>
    private RectTransform CreateLayer(string layerName)
    {
        var go = new GameObject(layerName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    /// <summary>Vuelve a colocar Overlays y Toasts al final tras anadir paneles de escena por codigo.</summary>
    public void KeepLayersOnTop() => EnsureLayers();

    private void EnsureLayers()
    {
        if (overlays == null)
        {
            Transform existing = transform.Find("Overlays");
            overlays = existing != null ? (RectTransform)existing : CreateLayer("Overlays");
        }
        if (toasts == null)
        {
            Transform existing = transform.Find("Toasts");
            toasts = existing != null ? (RectTransform)existing : CreateLayer("Toasts");
        }

        // Orden de dibujo: lo de la escena, luego Overlays y por ultimo Toasts
        overlays.SetAsLastSibling();
        toasts.SetAsLastSibling();
    }

    /// <summary>
    /// Padre donde crear un panel de escena (queda detras de Overlays y Toasts). Si la escena no tiene UiRoot,
    /// crea un canvas propio con la misma configuracion para no romper escenas sin migrar.
    /// </summary>
    public static RectTransform GetScreensParent(string fallbackName)
    {
        if (Instance != null) return Instance.Root;
        return CreateFallbackCanvas(fallbackName, 100);
    }

    /// <summary>Padre para paneles modales. Mismo respaldo que <see cref="GetScreensParent"/>.</summary>
    public static RectTransform GetOverlayParent(string fallbackName, int fallbackOrder)
    {
        if (Instance != null) return Instance.Overlays;
        return CreateFallbackCanvas(fallbackName, fallbackOrder);
    }

    private static RectTransform CreateFallbackCanvas(string canvasName, int order)
    {
        var go = new GameObject(canvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        return (RectTransform)go.transform;
    }
}

/// <summary>El juego admite vertical y horizontal (giro automatico, sin permitir la vertical invertida).</summary>
public static class MobileDisplay
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AllowRotation()
    {
        Screen.autorotateToPortrait = true;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}
