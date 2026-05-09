using UnityEngine;
using TMPro;
using DG.Tweening;

public class PopUpManager : MonoBehaviour
{
    public static PopUpManager Instance;

    [Header("Referencias")]
    public GameObject popUpPrefab;
    public Transform canvasParaPopUps;

    [Header("Ajustes Dinámicos")]
    [Tooltip("Margen en píxeles desde el borde físico de la pantalla")]
    public float paddingInPixels = 20f;

    private Camera mainCam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        mainCam = Camera.main; // Caché vital para el rendimiento
    }

    public void ShowPopUp(Vector3 worldPosition, string mensaje, Color colorTexto)
    {
        if (popUpPrefab == null || canvasParaPopUps == null) return;
        if (mainCam == null) mainCam = Camera.main;

        // 1. Posición cruda inicial
        Vector3 rawSpawnPos = worldPosition + new Vector3(0, 0.5f, -1f);

        // 2. Instanciamos el objeto ANTES de moverlo, para poder medir su texto
        GameObject popUp = Instantiate(popUpPrefab, rawSpawnPos, Quaternion.identity, canvasParaPopUps);

        TextMeshProUGUI textoUI = popUp.GetComponentInChildren<TextMeshProUGUI>();
        RectTransform rectTransform = popUp.GetComponent<RectTransform>();

        if (textoUI != null)
        {
            textoUI.text = mensaje;
            textoUI.color = colorTexto;

            // --- MAGIA SENIOR ---
            // Forzamos a TextMeshPro a recalcular su tamaño inmediatamente en este frame
            textoUI.ForceMeshUpdate();
        }

        // 3. Clamping Dinámico (Basado en la masa real del objeto)
        Vector3 safeSpawnPos = ClampWithScreenPhysics(rectTransform, rawSpawnPos);
        popUp.transform.position = safeSpawnPos;

        // 4. Animation and Lifecycle Management
        // Move UP relative to the safe position
        popUp.transform.DOMoveY(safeSpawnPos.y + 1f, 1.5f)
            .SetEase(Ease.OutCubic)
            // SECURITY/STABILITY: Link the tween to the GameObject. 
            // If the scene reloads and the object is destroyed, the tween kills itself.
            .SetLink(popUp)
            // PERFORMANCE: Destroy the object exactly when the animation finishes
            .OnComplete(() =>
            {
                if (popUp != null) Destroy(popUp);
            });
    }

    /// <summary>
    /// Calcula el ancho real del UI, lo proyecta a la pantalla del dispositivo
    /// y evita matemáticamente que los bordes del RectTransform crucen el Screen.width
    /// </summary>
    private Vector3 ClampWithScreenPhysics(RectTransform rt, Vector3 targetWorldPos)
    {
        if (mainCam == null) return targetWorldPos;

        // Traducimos el centro del objeto de 3D a píxeles 2D en la pantalla
        Vector3 screenPos = mainCam.WorldToScreenPoint(targetWorldPos);

        // Calculamos cuánto mide la MITAD del texto en unidades del mundo...
        float worldHalfWidth = (rt.rect.width * rt.lossyScale.x) / 2f;

        // ...y lo convertimos a un tamaño en píxeles de pantalla
        Vector3 rightEdgeScreen = mainCam.WorldToScreenPoint(targetWorldPos + Vector3.right * worldHalfWidth);
        float widthInPixels = Mathf.Abs(rightEdgeScreen.x - screenPos.x);

        // Definimos las paredes de la pantalla del celular de este jugador
        float screenLeftWall = paddingInPixels;
        float screenRightWall = Screen.width - paddingInPixels;

        // Si el "hombro izquierdo" del texto choca la pared izquierda, lo empujamos a la derecha
        if (screenPos.x - widthInPixels < screenLeftWall)
        {
            screenPos.x = screenLeftWall + widthInPixels;
        }
        // Si el "hombro derecho" choca la pared derecha, lo empujamos a la izquierda
        else if (screenPos.x + widthInPixels > screenRightWall)
        {
            screenPos.x = screenRightWall - widthInPixels;
        }

        // Convertimos la coordenada de píxeles corregida de vuelta al mundo 3D
        // ScreenToWorldPoint mantiene intacta la profundidad Z, ¡es matemáticamente perfecto!
        return mainCam.ScreenToWorldPoint(screenPos);
    }
}