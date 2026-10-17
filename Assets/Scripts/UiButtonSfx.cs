using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reproduce el sonido «Click» al pulsar un Button o Toggle, siempre a traves de AudioManager (asi respeta el volumen y
/// el mute de los ajustes). AudioManager lo anade solo a todos los botones de cada escena que se carga, de modo que ningun
/// boton tiene que cablear su propio sonido. Respeta la interactividad: un boton deshabilitado no suena.
/// </summary>
[DisallowMultipleComponent]
public class UiButtonSfx : MonoBehaviour, IPointerClickHandler
{
    public string soundName = "Click";

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        Selectable selectable = GetComponent<Selectable>();
        if (selectable != null && !selectable.IsInteractable()) return;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(soundName);
    }

    /// <summary>Anade el sonido a todos los Button y Toggle de las escenas cargadas (incluidos los inactivos) que aun no lo tengan.</summary>
    public static void AttachToAll()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Attach(button.gameObject);
        foreach (Toggle toggle in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Attach(toggle.gameObject);
    }

    public static void Attach(GameObject target)
    {
        if (target.GetComponent<UiButtonSfx>() == null) target.AddComponent<UiButtonSfx>();
    }
}
