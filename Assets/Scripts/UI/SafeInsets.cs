using UnityEngine;

/// <summary>
/// Margenes del area segura de la pantalla (notch, camara perforada, barra de gestos) expresados en unidades del canvas
/// raiz de la escena. Se recalculan cuando cambia <see cref="Screen.safeArea"/>, la resolucion o el ancho del canvas;
/// cada cambio incrementa <see cref="Version"/> para que los componentes sepan que deben reaplicarse.
/// </summary>
public static class SafeInsets
{
    private static Rect lastSafeArea;
    private static Vector2Int lastScreen;
    private static float lastCanvasWidth = -1f;

    public static float Top { get; private set; }
    public static float Bottom { get; private set; }
    public static float Left { get; private set; }
    public static float Right { get; private set; }

    /// <summary>Se incrementa cada vez que los margenes cambian.</summary>
    public static int Version { get; private set; }

    public static void Refresh(RectTransform canvasRect)
    {
        if (canvasRect == null) return;

        Rect safe = Screen.safeArea;
        // Dimensiones reales del canvas en pixeles (en el Editor Screen.* puede ir por detras de la ventana Game)
        Canvas canvas = canvasRect.GetComponent<Canvas>();
        Rect pixels = canvas != null ? canvas.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
        var screen = new Vector2Int(Mathf.RoundToInt(pixels.width), Mathf.RoundToInt(pixels.height));
        float canvasWidth = canvasRect.rect.width;
        if (safe == lastSafeArea && screen == lastScreen && Mathf.Approximately(canvasWidth, lastCanvasWidth)) return;

        lastSafeArea = safe;
        lastScreen = screen;
        lastCanvasWidth = canvasWidth;

        // Screen.safeArea solo es fiable si Screen.* coincide con el canvas; en el Editor, justo tras cambiar el tamano de
        // la ventana Game, van desfasados y los margenes saldrian absurdos: se ignoran hasta que coincidan
        bool consistent = Screen.width == screen.x && Screen.height == screen.y;
        if (!consistent) safe = new Rect(0f, 0f, screen.x, screen.y);

        // Un notch o una barra de gestos real nunca come mas de ~15 % de un borde; mas que eso es un area segura de otra
        // resolucion (simulador del Editor) y se ignora
        if (safe.yMin > screen.y * 0.15f || screen.y - safe.yMax > screen.y * 0.15f
            || safe.xMin > screen.x * 0.15f || screen.x - safe.xMax > screen.x * 0.15f)
        {
            safe = new Rect(0f, 0f, screen.x, screen.y);
        }

        // pixeles de pantalla -> unidades del canvas
        float pixelsToCanvas = screen.x > 0 ? canvasWidth / screen.x : 1f;
        // Se recortan a 0: en el Editor la ventana Game puede ir por detras de Screen.safeArea
        Left = Mathf.Max(0f, safe.xMin * pixelsToCanvas);
        Right = Mathf.Max(0f, (screen.x - safe.xMax) * pixelsToCanvas);
        Bottom = Mathf.Max(0f, safe.yMin * pixelsToCanvas);
        Top = Mathf.Max(0f, (screen.y - safe.yMax) * pixelsToCanvas);
        Version++;
    }
}
