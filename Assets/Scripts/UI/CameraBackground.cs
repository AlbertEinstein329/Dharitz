using UnityEngine;

/// <summary>
/// Fondo del tablero detras de todo el mundo 2D, sin usar un canvas: crea un SpriteRenderer hijo de la camara que cubre
/// siempre el encuadre (modo «cover», recorta lo que sobra) y se reajusta si cambia la relacion de aspecto o el zoom.
/// Sustituye al antiguo Canvas «Screen Space - Camera» con un RawImage.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraBackground : MonoBehaviour
{
    [SerializeField] private Texture2D texture;

    [Tooltip("Material sin iluminacion (Sprite-Unlit-Default de URP). Si falta, el fondo recibe la luz 2D como el resto de sprites.")]
    [SerializeField] private Material unlitMaterial;

    [SerializeField] private int sortingOrder = -1000;

    private Camera cam;
    private Transform backdrop;
    private float lastAspect;
    private float lastSize;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (texture == null) return;

        // 1 unidad de alto: la escala de ajuste se calcula sobre eso
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.height);

        var go = new GameObject("Background");
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        backdrop = go.transform;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        if (unlitMaterial != null) renderer.sharedMaterial = unlitMaterial;

        Fit();
    }

    private void LateUpdate()
    {
        if (backdrop == null) return;
        if (!Mathf.Approximately(lastAspect, cam.aspect) || !Mathf.Approximately(lastSize, cam.orthographicSize)) Fit();
    }

    private void Fit()
    {
        lastAspect = cam.aspect;
        lastSize = cam.orthographicSize;

        float viewHeight = cam.orthographicSize * 2f;
        float viewWidth = viewHeight * cam.aspect;
        float textureAspect = (float)texture.width / texture.height;

        // El sprite mide textureAspect x 1 unidades: se escala hasta cubrir ancho y alto
        float scale = Mathf.Max(viewHeight, viewWidth / textureAspect);
        backdrop.localScale = new Vector3(scale, scale, 1f);
        backdrop.localPosition = new Vector3(0f, 0f, Mathf.Min(cam.farClipPlane - 1f, 90f));
    }
}
