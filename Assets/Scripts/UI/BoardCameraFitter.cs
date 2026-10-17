using System.Collections;
using UnityEngine;

/// <summary>
/// Encuadra el tablero entre el HUD superior y el inferior en cualquier relacion de aspecto, moviendo y ajustando el zoom
/// de la camara (el tablero no cambia de tamano ni de posicion en el mundo, asi que la IA y sus sensores no se ven afectados).
/// Mide los bordes reales del HUD en pantalla, de modo que sirve igual en 16:9, 20:9 o tabletas.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BoardCameraFitter : MonoBehaviour
{
    [Tooltip("HUD que queda por encima del tablero (se usa su borde inferior).")]
    [SerializeField] private RectTransform topHud;

    [Tooltip("HUD que queda por debajo del tablero (se usa su borde superior).")]
    [SerializeField] private RectTransform bottomHud;

    [Tooltip("Tamano del tablero en unidades del mundo (ancho x alto). Se lee de GridManager si es cero.")]
    [SerializeField] private Vector2 boardSize = Vector2.zero;

    [Tooltip("Aire extra alrededor del tablero (fraccion del tamano).")]
    [SerializeField] private float margin = 0.04f;

    [Tooltip("Zoom minimo de la camara (el original de la escena).")]
    [SerializeField] private float minOrthographicSize = 10f;

    private Camera cam;
    private int lastWidth, lastHeight;
    private Vector2 lastTopHudPosition, lastBottomHudPosition;

    private void Awake() => cam = GetComponent<Camera>();

    private IEnumerator Start()
    {
        // Espera a que el canvas calcule su layout y GridManager genere el tablero
        yield return new WaitForEndOfFrame();
        Fit();
    }

    private void LateUpdate()
    {
        if (cam.pixelWidth != lastWidth || cam.pixelHeight != lastHeight || HudMoved()) Fit();
    }

    private bool HudMoved()
    {
        return topHud != null && (Vector2)topHud.position != lastTopHudPosition
            || bottomHud != null && (Vector2)bottomHud.position != lastBottomHudPosition;
    }

    public void Fit()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (topHud == null || bottomHud == null || cam.pixelHeight <= 0) return;

        lastWidth = cam.pixelWidth;
        lastHeight = cam.pixelHeight;
        lastTopHudPosition = topHud.position;
        lastBottomHudPosition = bottomHud.position;

        Vector2 size = boardSize;
        if (size == Vector2.zero)
        {
            GridManager grid = FindFirstObjectByType<GridManager>();
            if (grid == null) return;
            size = new Vector2(grid.cols * grid.cellSize, grid.rows * grid.cellSize);
        }

        // Banda libre en pixeles: del borde superior del HUD inferior al borde inferior del HUD superior
        float topEdgePx = ScreenMinY(topHud);
        float bottomEdgePx = ScreenMaxY(bottomHud);
        float bandPx = topEdgePx - bottomEdgePx;
        if (bandPx < cam.pixelHeight * 0.2f) return; // medidas absurdas (HUD aun sin layout): no tocar la camara

        // Zoom para que el alto del tablero (con margen) llene la banda; nunca menos que el ancho necesario ni que el original
        float sizeForHeight = size.y * (1f + margin) * cam.pixelHeight / (2f * bandPx);
        float sizeForWidth = size.x * (1f + margin) / (2f * cam.aspect);
        float orthographic = Mathf.Max(minOrthographicSize, sizeForHeight, sizeForWidth);
        cam.orthographicSize = orthographic;

        // Desplazamiento vertical: el centro del tablero (origen) debe quedar en el centro de la banda
        float bandCenterPx = (topEdgePx + bottomEdgePx) * 0.5f;
        float worldPerPixel = 2f * orthographic / cam.pixelHeight;
        Vector3 position = transform.position;
        position.y = -(bandCenterPx - cam.pixelHeight * 0.5f) * worldPerPixel;
        transform.position = position;
    }

    private static float ScreenMinY(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners); // canvas Overlay: las esquinas ya estan en pixeles de pantalla
        return Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
    }

    private static float ScreenMaxY(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
    }
}
