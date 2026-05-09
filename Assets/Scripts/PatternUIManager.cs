using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening; // Esencial para la animación fluida

public class PatternUIManager : MonoBehaviour
{
    [Header("Sistema Retráctil")]
    [Tooltip("El RectTransform del panel completo que se deslizará")]
    public RectTransform animatablePanel;
    public float expandedPosition = 0f; // Posición Y cuando está a la vista
    public float retractedPosition = 250f; // Posición Y cuando se esconde hacia arriba (ajústalo según tu panel)
    private bool isExpanded = false; // El panel arranca cerrado por defecto

    [Header("Contenedores (2 Filas)")]
    public Transform row1Container; // Para patrones 1, 2 y 3
    public Transform row2Container; // Para patrones 4, 5 y 6

    [Header("Prefabs y Ajustes")]
    public GameObject patternContainerPrefab;
    public GameObject dieUIPrefab;
    public float dieSize = 30f;
    public float spacing = 2f;
    public Sprite[] diceSprites;

    void Start()
    {
        Invoke(nameof(GeneratePanel), 0.1f);

        // Colocamos el panel en su estado inicial (retraído) inmediatamente
        if (animatablePanel != null)
        {
            animatablePanel.anchoredPosition = new Vector2(animatablePanel.anchoredPosition.x, retractedPosition);
        }
    }

    // Llama a esta función desde el botón de "Abrir/Cerrar"
    public void TogglePanel()
    {
        if (animatablePanel == null) return;

        isExpanded = !isExpanded;
        float targetY = isExpanded ? expandedPosition : retractedPosition;

        // Animación suave con DOTween
        animatablePanel.DOAnchorPosY(targetY, 0.4f).SetEase(Ease.OutCubic);
    }

    public void GeneratePanel()
    {
        VariantData variante = GameManager.Instance.currentVariant;
        if (variante == null) return;

        // Limpiamos ambas filas
        foreach (Transform child in row1Container) Destroy(child.gameObject);
        foreach (Transform child in row2Container) Destroy(child.gameObject);

        for (int i = 1; i <= 6; i++)
        {
            PatternData patron = variante.GetPattern(i);
            if (patron == null || patron.baseShape.Count == 0) continue;

            // --- LÓGICA DE 2 FILAS ---
            // Patrones del 1 al 3 van a la Fila 1, del 4 al 6 van a la Fila 2
            Transform padreCorrespondiente = (i <= 3) ? row1Container : row2Container;

            GameObject grupoPatron = Instantiate(patternContainerPrefab, padreCorrespondiente);
            RectTransform rectGrupo = grupoPatron.GetComponent<RectTransform>();

            List<Vector2Int> formaNormalizada = Normalize(patron.baseShape);

            int maxX = formaNormalizada.Max(v => v.x);
            int maxY = formaNormalizada.Max(v => v.y);

            float offsetStep = dieSize + spacing;
            rectGrupo.sizeDelta = new Vector2((maxX + 1) * offsetStep, (maxY + 1) * offsetStep);

            foreach (Vector2Int pos in formaNormalizada)
            {
                GameObject dadoInst = Instantiate(dieUIPrefab, grupoPatron.transform);
                RectTransform rectDado = dadoInst.GetComponent<RectTransform>();
                Image imgDado = dadoInst.GetComponent<Image>();

                if (diceSprites.Length >= i && diceSprites[i - 1] != null)
                {
                    imgDado.sprite = diceSprites[i - 1];
                }

                rectDado.sizeDelta = new Vector2(dieSize, dieSize);

                float posX = pos.x * offsetStep;
                float posY = pos.y * offsetStep;

                rectDado.anchoredPosition = new Vector2(posX, posY);
            }
        }
    }

    private List<Vector2Int> Normalize(List<Vector2Int> points)
    {
        if (points.Count == 0) return points;
        int minX = points.Min(p => p.x);
        int minY = points.Min(p => p.y);
        return points.Select(p => new Vector2Int(p.x - minX, p.y - minY)).ToList();
    }
}