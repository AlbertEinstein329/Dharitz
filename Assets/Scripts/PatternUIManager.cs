using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class PatternUIManager : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("El panel horizontal donde irán todos los patrones")]
    public Transform contenedorPrincipal;
    [Tooltip("Un prefab de un RectTransform vacío (Anclaje Inferior-Izquierdo)")]
    public GameObject prefabContenedorPatron;
    [Tooltip("Un prefab con componente Image (Anclaje Inferior-Izquierdo)")]
    public GameObject prefabDadoUI;

    [Header("Ajustes Visuales")]
    public float tamanoDado = 30f;
    public float espaciado = 2f;
    [Tooltip("Pon aquí los 6 sprites de los dados genéricos (sin color) del 1 al 6")]
    public Sprite[] spritesDados;

    void Start()
    {
        // Usamos un pequeño delay para asegurarnos de que el GameManager 
        // ya haya cargado el SessionConfig en su propio Awake/Start.
        Invoke(nameof(GenerarPanel), 0.1f);
    }

    public void GenerarPanel()
    {
        VariantData variante = GameManager.Instance.varianteActual;
        if (variante == null) return;

        // 1. Limpiamos cualquier prueba previa en el panel
        foreach (Transform child in contenedorPrincipal)
        {
            Destroy(child.gameObject);
        }

        // 2. Iteramos del 1 al 6
        for (int i = 1; i <= 6; i++)
        {
            PatternData patron = variante.ObtenerPatron(i);
            if (patron == null || patron.baseShape.Count == 0) continue;

            // Instanciamos el "bloque" que contendrá este patrón específico
            GameObject grupoPatron = Instantiate(prefabContenedorPatron, contenedorPrincipal);
            RectTransform rectGrupo = grupoPatron.GetComponent<RectTransform>();

            // Normalizamos para asegurar que la figura empiece en (0,0) local
            List<Vector2Int> formaNormalizada = Normalize(patron.baseShape);

            // Calculamos cuánto medirá este bloque para que el Horizontal Layout Group no lo aplaste
            int maxX = formaNormalizada.Max(v => v.x);
            int maxY = formaNormalizada.Max(v => v.y);

            float offsetStep = tamanoDado + espaciado;
            rectGrupo.sizeDelta = new Vector2((maxX + 2) * offsetStep, (maxY + 2) * offsetStep);

            // 3. Ensamblamos los "cuadritos" del dado
            foreach (Vector2Int pos in formaNormalizada)
            {
                GameObject dadoInst = Instantiate(prefabDadoUI, grupoPatron.transform);
                RectTransform rectDado = dadoInst.GetComponent<RectTransform>();
                Image imgDado = dadoInst.GetComponent<Image>();

                // Asignamos el sprite (índice i-1 porque array empieza en 0)
                if (spritesDados.Length >= i && spritesDados[i - 1] != null)
                {
                    imgDado.sprite = spritesDados[i - 1];
                }

                rectDado.sizeDelta = new Vector2(tamanoDado, tamanoDado);

                // Mapeo matemático de la Matriz (X, Y) a Píxeles UI
                // Importante: Asume que las "x" son columnas (derecha) y las "y" filas (arriba/abajo)
                float posX = pos.x * offsetStep;
                float posY = pos.y * offsetStep;

                rectDado.anchoredPosition = new Vector2(posX, posY);
            }
        }
    }

    // Normaliza las coordenadas para que la pieza más baja/izquierda sea siempre (0,0)
    private List<Vector2Int> Normalize(List<Vector2Int> points)
    {
        if (points.Count == 0) return points;
        int minX = points.Min(p => p.x);
        int minY = points.Min(p => p.y);
        return points.Select(p => new Vector2Int(p.x - minX, p.y - minY)).ToList();
    }
}