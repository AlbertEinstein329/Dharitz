using System.Collections;
using UnityEngine;

/// <summary>
/// Encuadra el tablero en el espacio libre que dejan los HUD, en vertical y en horizontal, moviendo y ajustando el zoom de
/// la camara (el tablero no cambia de tamano ni de posicion en el mundo, asi que la IA y sus sensores no se ven afectados).
/// Vertical: entre el HUD superior y el inferior. Horizontal: entre el HUD izquierdo y el derecho, a toda la altura.
/// Mide los bordes reales del HUD en pantalla, de modo que sirve igual en 16:9, 20:9 o tabletas.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BoardCameraFitter : MonoBehaviour
{
    [Header("Pantalla vertical")]
    [Tooltip("HUD que queda por encima del tablero (se usa su borde inferior).")]
    [SerializeField] private RectTransform topHud;

    [Tooltip("HUD que queda por debajo del tablero (se usa su borde superior).")]
    [SerializeField] private RectTransform bottomHud;

    [Header("Pantalla horizontal")]
    [Tooltip("HUD a la izquierda del tablero (se usa su borde derecho).")]
    [SerializeField] private RectTransform leftHud;

    [Tooltip("HUD a la derecha del tablero (se usa su borde izquierdo).")]
    [SerializeField] private RectTransform rightHud;

    [Header("Ajustes")]
    [Tooltip("Tamano del tablero en unidades del mundo (ancho x alto). Se lee de GridManager si es cero.")]
    [SerializeField] private Vector2 boardSize = Vector2.zero;

    [Tooltip("Aire extra alrededor del tablero (fraccion del tamano).")]
    [SerializeField] private float margin = 0.04f;

    [Tooltip("Zoom minimo de la camara (el original de la escena).")]
    [SerializeField] private float minOrthographicSize = 10f;

    private Camera cam;
    private int lastWidth, lastHeight;
    private Vector4 lastHudState;

    private void Awake() => cam = GetComponent<Camera>();

    private IEnumerator Start()
    {
        // Espera a que el canvas calcule su layout y GridManager genere el tablero
        yield return new WaitForEndOfFrame();
        Fit();
    }

    private void LateUpdate()
    {
        if (cam.pixelWidth != lastWidth || cam.pixelHeight != lastHeight || HudState() != lastHudState) Fit();
    }

    // Posiciones y tamanos de los HUD que intervienen: si alguno se mueve (giro de pantalla, area segura) se re-encuadra
    private Vector4 HudState()
    {
        return new Vector4(Sig(topHud) + Sig(bottomHud), Sig(leftHud) + Sig(rightHud), 0f, 0f);
    }

    private static float Sig(RectTransform rect) => rect == null ? 0f : rect.position.x * 1.31f + rect.position.y * 1.73f + rect.rect.width * rect.lossyScale.x * 0.37f + rect.rect.height * rect.lossyScale.y * 0.53f;

    public void Fit()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam.pixelHeight <= 0 || cam.pixelWidth <= 0) return;

        lastWidth = cam.pixelWidth;
        lastHeight = cam.pixelHeight;
        lastHudState = HudState();

        Vector2 size = boardSize;
        if (size == Vector2.zero)
        {
            GridManager grid = FindFirstObjectByType<GridManager>();
            if (grid == null) return;
            size = new Vector2(grid.cols * grid.cellSize, grid.rows * grid.cellSize);
        }

        float width = cam.pixelWidth, height = cam.pixelHeight;
        bool landscape = width > height;

        // Rectangulo libre en pixeles de pantalla
        float left, right, bottom, top;
        if (landscape)
        {
            if (leftHud == null || rightHud == null) return;
            left = MaxX(leftHud);
            right = MinX(rightHud);
            bottom = height * 0.03f;
            top = height * 0.97f;
        }
        else
        {
            if (topHud == null || bottomHud == null) return;
            left = 0f;
            right = width;
            top = MinY(topHud);
            bottom = MaxY(bottomHud);
        }

        float bandWidth = right - left, bandHeight = top - bottom;
        if (bandHeight < height * 0.2f || bandWidth < width * 0.2f) return; // medidas absurdas (HUD aun sin layout): no tocar la camara

        // Zoom para que el tablero (con margen) quepa en la banda en alto y en ancho; nunca menos que el original
        float sizeForHeight = size.y * (1f + margin) * height / (2f * bandHeight);
        float sizeForWidth = size.x * (1f + margin) * height / (2f * bandWidth);
        // En vertical el zoom no baja del original; en horizontal el tablero se acerca todo lo que permita el espacio libre
        float minimumSize = landscape ? 2f : minOrthographicSize;
        float orthographic = Mathf.Max(minimumSize, sizeForHeight, sizeForWidth);
        cam.orthographicSize = orthographic;

        // Desplazamiento: el centro del tablero (origen) debe quedar en el centro de la banda libre
        float worldPerPixel = 2f * orthographic / height;
        Vector3 position = transform.position;
        position.x = -((left + right) * 0.5f - width * 0.5f) * worldPerPixel;
        position.y = -((bottom + top) * 0.5f - height * 0.5f) * worldPerPixel;
        transform.position = position;
    }

    private static float MinY(RectTransform rect) { Corners(rect, out _, out _, out float minY, out _); return minY; }
    private static float MaxY(RectTransform rect) { Corners(rect, out _, out _, out _, out float maxY); return maxY; }
    private static float MinX(RectTransform rect) { Corners(rect, out float minX, out _, out _, out _); return minX; }
    private static float MaxX(RectTransform rect) { Corners(rect, out _, out float maxX, out _, out _); return maxX; }

    private static void Corners(RectTransform rect, out float minX, out float maxX, out float minY, out float maxY)
    {
        var c = new Vector3[4];
        rect.GetWorldCorners(c); // canvas Overlay: las esquinas ya estan en pixeles de pantalla
        minX = Mathf.Min(c[0].x, c[1].x, c[2].x, c[3].x);
        maxX = Mathf.Max(c[0].x, c[1].x, c[2].x, c[3].x);
        minY = Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y);
        maxY = Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y);
    }
}
