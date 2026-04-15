using UnityEngine;
using UnityEngine.UI; // Cambia a TMPro si usas TextMeshPro
using DG.Tweening;
using TMPro;

public class PopUpManager : MonoBehaviour
{
    public static PopUpManager Instance;

    [Header("Referencias")]
    public GameObject popUpPrefab;
    [Tooltip("El Canvas en modo World Space donde vivirán los textos")]
    public Transform canvasParaPopUps; // <-- NUEVO: El contenedor

    private void Awake() { if (Instance == null) Instance = this; }

    public void MostrarPopUp(Vector3 worldPosition, string mensaje, Color colorTexto)
    {
        if (popUpPrefab == null || canvasParaPopUps == null)
        {
            Debug.LogWarning("Falta asignar el Prefab o el Canvas en el PopUpManager");
            return;
        }

        // Instanciar un poco más arriba del dado, y lo hacemos HIJO del Canvas
        Vector3 spawnPos = worldPosition + new Vector3(0, 0.5f, -1f); // El Z -1 ayuda a que se vea por delante del dado

        // ¡LA MAGIA AQUÍ! Añadimos 'canvasParaPopUps' como cuarto parámetro
        GameObject popUp = Instantiate(popUpPrefab, spawnPos, Quaternion.identity, canvasParaPopUps);

        TMPro.TextMeshProUGUI textoUI = popUp.GetComponentInChildren<TMPro.TextMeshProUGUI>();


        if (textoUI != null)
        {
            textoUI.text = mensaje;
            textoUI.color = colorTexto;
        }

        // Animación de DOTween
        popUp.transform.DOMoveY(spawnPos.y + 1f, 1.5f).SetEase(Ease.OutCubic);

        // Opcional: Fade out (desvanecimiento) si el texto lo soporta
        textoUI.DOFade(0, 1.5f).SetEase(Ease.InExpo);

        Destroy(popUp, 1.6f);
    }
}