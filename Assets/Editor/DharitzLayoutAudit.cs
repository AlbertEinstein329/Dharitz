using System.Text;
using UnityEngine;

/// <summary>
/// Comprobacion numerica del encuadre del tablero en Play (no depende de capturas, que en el Editor pueden salir
/// desfasadas al cambiar la resolucion de la ventana Game). Mide en pixeles del Game view donde quedan el tablero,
/// el HUD superior y el inferior, y avisa de solapes o recortes.
/// </summary>
public static class DharitzLayoutAudit
{
    public static string Report()
    {
        Camera cam = Camera.main;
        var grid = Object.FindFirstObjectByType<GridManager>();
        var fitter = Object.FindFirstObjectByType<BoardCameraFitter>();
        if (cam == null || grid == null || fitter == null) return "Sin camara, GridManager o BoardCameraFitter (hay que estar en Play en Tablero).";

        var so = new UnityEditor.SerializedObject(fitter);
        var top = (RectTransform)so.FindProperty("topHud").objectReferenceValue;
        var bottom = (RectTransform)so.FindProperty("bottomHud").objectReferenceValue;

        float w = cam.pixelWidth, h = cam.pixelHeight;
        // Tablero: centrado en el origen con celdas de cellSize (modo entrenamiento) -> esquinas en el mundo
        float halfW = grid.cols * grid.cellSize * 0.5f, halfH = grid.rows * grid.cellSize * 0.5f;
        Vector3 bl = cam.WorldToScreenPoint(new Vector3(-halfW, -halfH, 0f));
        Vector3 tr = cam.WorldToScreenPoint(new Vector3(halfW, halfH, 0f));

        var leftHud = so.FindProperty("leftHud").objectReferenceValue as RectTransform;
        var rightHud = so.FindProperty("rightHud").objectReferenceValue as RectTransform;
        float topHudBottom = MinY(top), bottomHudTop = MaxY(bottom);
        var sb = new StringBuilder();
        sb.Append($"{w}x{h} (aspect {w / h:F3}) camSize={cam.orthographicSize:F2} camY={cam.transform.position.y:F2} | ");
        sb.Append($"tablero px x[{bl.x:F0},{tr.x:F0}] y[{bl.y:F0},{tr.y:F0}] | HUD sup baja a y={topHudBottom:F0}, HUD inf sube a y={bottomHudTop:F0} | ");

        bool leftOk = bl.x >= 0f && tr.x <= w;
        float overlapTop = tr.y - topHudBottom;        // >0: el tablero entra bajo el HUD superior
        float overlapBottom = bottomHudTop - bl.y;     // >0: el tablero entra sobre el HUD inferior
        sb.Append($"ancho {(leftOk ? "OK" : "RECORTADO")}, solape sup {overlapTop:F0}px, solape inf {overlapBottom:F0}px");
        if (w > h && leftHud != null && rightHud != null)
        {
            // Horizontal: el tablero debe quedar entre el HUD izquierdo y el derecho
            var lc = new Vector3[4]; leftHud.GetWorldCorners(lc);
            var rc = new Vector3[4]; rightHud.GetWorldCorners(rc);
            float leftEdge = Mathf.Max(lc[0].x, lc[2].x), rightEdge = Mathf.Min(rc[0].x, rc[2].x);
            sb.Append($" | HORIZONTAL: HUD izq hasta x={leftEdge:F0}, HUD der desde x={rightEdge:F0}, holgura izq {bl.x - leftEdge:F0}px, der {rightEdge - tr.x:F0}px, alto tablero {tr.y - bl.y:F0} de {h:F0}");
        }
        return sb.ToString();
    }

    private static float MinY(RectTransform r)
    {
        var c = new Vector3[4];
        r.GetWorldCorners(c);
        return Mathf.Min(c[0].y, c[1].y, c[2].y, c[3].y);
    }

    private static float MaxY(RectTransform r)
    {
        var c = new Vector3[4];
        r.GetWorldCorners(c);
        return Mathf.Max(c[0].y, c[1].y, c[2].y, c[3].y);
    }
}
