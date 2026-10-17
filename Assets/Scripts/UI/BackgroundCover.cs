using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fondos de pantalla completa que conservan su proporcion: cubren toda la pantalla recortando lo que sobra en lugar de
/// estirarse (un fondo 9:16 estirado a 9:20 se deforma un 20 %). Se usa en Login, menu y paneles online.
/// </summary>
public static class BackgroundCover
{
    /// <summary>Crea un fondo «cover» como primer hijo de <paramref name="parent"/>.</summary>
    public static Image Create(Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject("BackgroundCover", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;

        var fitter = go.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 9f / 16f;
        return image;
    }

    /// <summary>
    /// Sustituye el fondo estirado de un panel por uno «cover»: apaga el Image original (que puede ser a la vez el
    /// contenedor de un layout) y pone el nuevo detras de todo el panel.
    /// </summary>
    public static void Replace(Image stretched)
    {
        if (stretched == null || stretched.sprite == null) return;

        Transform panel = stretched.transform;
        // Si el Image es el propio contenedor del layout, el fondo va en su padre para no entrar en el layout
        Transform parent = stretched.GetComponent<LayoutGroup>() != null && stretched.transform.parent != null ? stretched.transform.parent : panel;

        Create(parent, stretched.sprite, stretched.color);
        stretched.enabled = false;
    }
}
